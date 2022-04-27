using System;
using System.Linq;
using EventStore.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace EventStore.Projections.Core.Services.Processing {
	public class PartitionState {
		private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings {
			DateParseHandling = DateParseHandling.None,
		};
		
		public bool IsChanged(PartitionState newState) {
			return State != newState.State || Result != newState.Result;
		}

		public static PartitionState Deserialize(string serializedState, CheckpointTag causedBy) {
			if (serializedState == null)
				return new PartitionState("", null, causedBy);

			JToken state = null;
			JToken result = null;

			if (!string.IsNullOrEmpty(serializedState)) {
				var deserialized = JsonConvert.DeserializeObject(serializedState, JsonSettings);
				var array = deserialized as JArray;
				if (array != null && array.Count > 0) {
					state = array[0] as JToken;
					if (array.Count == 2) {
						result = array[1] as JToken;
					}
				} else {
					state = deserialized as JObject;
				}
			}

			var stateJson = state != null ? state.ToCanonicalJson() : "";
			var resultJson = result != null ? result.ToCanonicalJson() : null;

			return new PartitionState(stateJson, serializedState, resultJson, causedBy);
		}

		private static void Error(JsonTextReader reader, string message) {
			throw new Exception(string.Format("{0} (At: {1}, {2})", message, reader.LineNumber, reader.LinePosition));
		}

		private readonly string _state;
		private readonly string _result;
		private readonly string _serializedState;
		private readonly CheckpointTag _causedBy;

		private PartitionState(string state, string serializedState, string result, CheckpointTag causedBy) {
			if (state == null) throw new ArgumentNullException("state");
			if (causedBy == null) throw new ArgumentNullException("causedBy");

			if (string.IsNullOrEmpty(serializedState) || serializedState[0] != '[' && serializedState[serializedState.Length-1] != ']') {
				_serializedState = $"[{state}]";
			} else {
				_serializedState = serializedState;
			}

			_state = state;
			_result = result;
			_causedBy = causedBy;
		}
		

		public PartitionState(string state, string result, CheckpointTag causedBy) : this(state, null, result, causedBy) {
			
		}

		public string State {
			get { return _state; }
		}

		public CheckpointTag CausedBy {
			get { return _causedBy; }
		}

		public string Result {
			get { return _result; }
		}

		public string Serialize() {
			var state = _state;
			if (state == "" && Result != null)
				throw new Exception("state == \"\" && Result != null");

			return Result == null ? _serializedState : $"[{state},{Result}]";
		}
	}
}
