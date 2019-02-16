using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace EventStore.Projections.Core.Services.v8 {
	public class V8ProjectionStateHandler : IProjectionStateHandler {
		private readonly PreludeScript _prelude;
		private readonly QueryScript _query;
		private List<EmittedEventEnvelope> _emittedEvents;
		private CheckpointTag _eventPosition;
		private bool _disposed;

		public V8ProjectionStateHandler(
			string preludeName, string querySource, Func<string, Tuple<string, string>> getModuleSource,
			Action<string, object[]> logger, Action<int, Action> cancelCallbackFactory) {
			var preludeSource = getModuleSource(preludeName);
			var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource,
				cancelCallbackFactory, logger);
			QueryScript query;
			try {
				query = new QueryScript(prelude, querySource, "POST-BODY");
				query.Emit += QueryOnEmit;
			} catch {
				prelude.Dispose(); // clean up unmanaged resources if failed to create
				throw;
			}

			_prelude = prelude;
			_query = query;
		}

		[DataContract]
		public class EmittedEventJsonContract {
			[DataMember] public string streamId;

			[DataMember] public string eventName;

			[DataMember] public bool isJson;

			[DataMember] public string body;

			[DataMember] public Dictionary<string, JRaw> metadata;

			public ExtraMetaData GetExtraMetadata() {
				if (metadata == null)
					return null;
				return new ExtraMetaData(metadata);
			}
		}


		private void QueryOnEmit(string json) {
			EmittedEventJsonContract emittedEvent;
			try {
				emittedEvent = json.ParseJson<EmittedEventJsonContract>();
			} catch (Exception ex) {
				throw new ArgumentException("Failed to deserialize emitted event JSON", ex);
			}

			if (_emittedEvents == null)
				_emittedEvents = new List<EmittedEventEnvelope>();
			_emittedEvents.Add(
				new EmittedEventEnvelope(
					new EmittedDataEvent(
						emittedEvent.streamId, Guid.NewGuid(), emittedEvent.eventName, emittedEvent.isJson,
						emittedEvent.body,
						emittedEvent.GetExtraMetadata(), _eventPosition, expectedTag: null)));
		}

		private QuerySourcesDefinition GetQuerySourcesDefinition() {
			CheckDisposed();
			var sourcesDefinition = _query.GetSourcesDefintion();
			if (sourcesDefinition == null)
				throw new InvalidOperationException("Invalid query.  No source definition.");
			return sourcesDefinition;
		}

		public void Load(string state) {
			CheckDisposed();
			_query.SetState(state);
		}

		public void LoadShared(string state) {
			CheckDisposed();
			_query.SetSharedState(state);
		}

		public void Initialize() {
			CheckDisposed();
			_query.Initialize();
		}

		public void InitializeShared() {
			CheckDisposed();
			_query.InitializeShared();
		}

		public string GetStatePartition(
			CheckpointTag eventPosition, string category, ResolvedEvent @event) {
			CheckDisposed();
			if (@event == null) throw new ArgumentNullException("event");
			var partition = _query.GetPartition(
				@event.Data.Trim(), // trimming data passed to a JS 
				new string[] {
					@event.EventStreamId, @event.IsJson ? "1" : "", @event.EventType, category ?? "",
					@event.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), @event.Metadata ?? "",
					@event.PositionMetadata ?? ""
				});
			if (partition == "")
				return null;
			else
				return partition;
		}

		public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data) {
			CheckDisposed();
			if (data == null) throw new ArgumentNullException("data");

			return _query.TransformCatalogEvent(
				(data.Data ?? "").Trim(), // trimming data passed to a JS 
				new[] {
					data.IsJson ? "1" : "", data.EventStreamId, data.EventType ?? "", "",
					data.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), data.Metadata ?? "",
					data.PositionMetadata ?? "", data.EventStreamId, data.StreamMetadata ?? ""
				});
		}

		public bool ProcessEvent(
			string partition, CheckpointTag eventPosition, string category, ResolvedEvent data, out string newState,
			out string newSharedState, out EmittedEventEnvelope[] emittedEvents) {
			CheckDisposed();
			_eventPosition = eventPosition;
			_emittedEvents = null;
			Tuple<string, string> newStates = null;
			if (data == null || data.Data == null) {
				newStates = _query.Push(
					"",
					new string[] { });
			} else {
				newStates = _query.Push(
					data.Data.Trim(), // trimming data passed to a JS 
					new[] {
						data.IsJson ? "1" : "", data.EventStreamId, data.EventType, category ?? "",
						data.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), data.Metadata ?? "",
						data.PositionMetadata ?? "", partition, ""
					});
			}

			newState = newStates.Item1;
			newSharedState = newStates.Item2;
/*            try
            {
                if (!string.IsNullOrEmpty(newState))
                {
                    var jo = newState.ParseJson<JObject>();
                }

            }
            catch (InvalidCastException)
            {
                Console.Error.WriteLine(newState);
            }
            catch (JsonException)
            {
                Console.Error.WriteLine(newState);
            }*/
			emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
			return true;
		}

		public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data,
			out EmittedEventEnvelope[] emittedEvents) {
			CheckDisposed();
			_eventPosition = createPosition;
			_emittedEvents = null;
			if (data == null || data.Data == null) {
				emittedEvents = null;
				return true;
			}

			_query.NotifyCreated(
				data.Data.Trim(), // trimming data passed to a JS 
				new[] {
					data.IsJson ? "1" : "", data.EventStreamId, data.EventType, "",
					data.EventSequenceNumber.ToString(CultureInfo.InvariantCulture), data.Metadata ?? "",
					data.PositionMetadata ?? "", partition, ""
				});
			emittedEvents = _emittedEvents == null ? null : _emittedEvents.ToArray();
			return true;
		}

		public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState) {
			CheckDisposed();
			_eventPosition = deletePosition;
			_emittedEvents = null;
			var newStates = _query.NotifyDeleted(
				"", // trimming data passed to a JS 
				new[] {
					partition, "" /* isSoftDedleted */
				});
			newState = newStates;
			return true;
		}

		public string TransformStateToResult() {
			CheckDisposed();
			var result = _query.TransformStateToResult();
			return result;
		}

		private void CheckDisposed() {
			if (_disposed)
				throw new InvalidOperationException("Disposed");
		}

		public void Dispose() {
			_disposed = true;
			if (_query != null)
				_query.Dispose();
			if (_prelude != null)
				_prelude.Dispose();
		}

		public IQuerySources GetSourceDefinition() {
			return GetQuerySourcesDefinition();
		}
	}
}
