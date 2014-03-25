using System;
using System.IO;
using EventStore.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class PartitionState
    {
        public bool IsChanged(PartitionState newState)
        {
            return State != newState.State || Result != newState.Result;
        }

        public static PartitionState Deserialize(string serializedState, CheckpointTag causedBy)
        {
            if (serializedState == null)
                return new PartitionState("", null, causedBy);

            JToken state = null;
            JToken result = null;

            if (!string.IsNullOrEmpty(serializedState))
            {
                var reader = new JsonTextReader(new StringReader(serializedState));

                if (!reader.Read())
                    Error(reader, "StartArray or StartObject expected");

                if (reader.TokenType == JsonToken.StartObject)
                    // old state format
                    state = JToken.ReadFrom(reader);
                else
                {
                    if (reader.TokenType != JsonToken.StartArray)
                        Error(reader, "StartArray expected");
                    if (!reader.Read() || (reader.TokenType != JsonToken.StartObject && reader.TokenType != JsonToken.EndArray))
                        Error(reader, "StartObject or EndArray expected");
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        state = JToken.ReadFrom(reader);
                        if (!reader.Read())
                            Error(reader, "StartObject or EndArray expected");
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            result = JToken.ReadFrom(reader);
                            if (!reader.Read())
                                Error(reader, "EndArray expected");
                        }
                        if (reader.TokenType != JsonToken.EndArray)
                            Error(reader, "EndArray expected");
                    }
                }
            }
            var stateJson = state != null ? state.ToCanonicalJson() :"";
            var resultJson = result != null ? result.ToCanonicalJson() : null;

            return new PartitionState(stateJson, resultJson, causedBy);
        }

        private static void Error(JsonTextReader reader, string message)
        {
            throw new Exception(string.Format("{0} (At: {1}, {2})", message, reader.LineNumber, reader.LinePosition));
        }

        private readonly string _state;
        private readonly string _result;
        private readonly CheckpointTag _causedBy;

        public PartitionState(string state, string result, CheckpointTag causedBy)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (causedBy == null) throw new ArgumentNullException("causedBy");

            _state = state;
            _result = result;
            _causedBy = causedBy;
        }

        public string State
        {
            get { return _state; }
        }

        public CheckpointTag CausedBy
        {
            get { return _causedBy; }
        }

        public string Result
        {
            get { return _result; }
        }

        public string Serialize()
        {
            var state = _state;
            if (state == "" && Result != null)
                throw new Exception("state == \"\" && Result != null");
            return Result != null
                       ? "[" + state + "," + _result + "]"
                       : "[" + state + "]";
        }
    }
}
