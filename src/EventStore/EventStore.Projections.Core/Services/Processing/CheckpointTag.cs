// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CheckpointTag : IComparable<CheckpointTag>
    {
        public readonly EventPosition Position;
        public readonly Dictionary<string, int> Streams;

        internal enum Mode
        {
            Position,
            Stream,
            MultiStream,
            PreparePosition
        }

        private CheckpointTag(EventPosition position, Dictionary<string, int> streams)
        {
            Position = position;
            Streams = streams;
            Mode_ = CalculateMode();
        }

        private CheckpointTag(long preparePosition)
        {
            Position = new EventPosition(long.MinValue, preparePosition);
            Mode_ = CalculateMode();
        }

        private CheckpointTag(EventPosition position)
        {
            Position = position;
            Mode_ = CalculateMode();
        }

        private CheckpointTag(Dictionary<string, int> streams)
        {
            foreach (var stream in streams)
            {
                if (stream.Key == "") throw new ArgumentException("Empty stream name", "streams");
                if (stream.Value < 0 && stream.Value != ExpectedVersion.NoStream) throw new ArgumentException("Invalid sequence number", "streams");
            }
            Streams = new Dictionary<string, int>(streams); // clone
            Position = new EventPosition(Int64.MinValue, Int64.MinValue);
            Mode_ = CalculateMode();
        }

        private CheckpointTag(string stream, int sequenceNumber)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (stream == "") throw new ArgumentException("stream");
            if (sequenceNumber < 0 && sequenceNumber != ExpectedVersion.NoStream) throw new ArgumentException("sequenceNumber");
            Position = new EventPosition(Int64.MinValue, Int64.MinValue);
            Streams = new Dictionary<string, int> {{stream, sequenceNumber}};
            Mode_ = CalculateMode();
        }

        private Mode CalculateMode()
        {
            if (Streams == null || Streams.Count == 0)
                if (CommitPosition == null && PreparePosition != null)
                    return Mode.PreparePosition;
                else 
                    return Mode.Position;
            if (Streams.Count == 1)
                return Mode.Stream;
            return Mode.MultiStream;
        }

        public static bool operator >(CheckpointTag left, CheckpointTag right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (!ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;
            if (ReferenceEquals(left, null) && !ReferenceEquals(right, null))
                return false;
            var leftMode = left.Mode_;
            var rightMode = right.Mode_;
            UpgradeModes(ref leftMode, ref rightMode);
            if (leftMode != rightMode)
                throw new NotSupportedException("Cannot compare checkpoint tags in different modes");
            switch (leftMode)
            {
                case Mode.Position:
                    return left.Position > right.Position;
                case Mode.PreparePosition:
                    return left.PreparePosition > right.PreparePosition;
                case Mode.Stream:
                    if (left.Streams.Keys.First() != right.Streams.Keys.First())
                        throw new InvalidOperationException("Cannot compare checkpoint tags across different streams");
                    var result = left.Streams.Values.First() > right.Streams.Values.First();
                    return result;
                case Mode.MultiStream:
                    int rvalue;
                    bool anyLeftGreater = left.Streams.Any(l => !right.Streams.TryGetValue(l.Key, out rvalue) || l.Value > rvalue);

                    int lvalue;
                    bool anyRightGreater = right.Streams.Any(r => !left.Streams.TryGetValue(r.Key, out lvalue) || r.Value > lvalue);

                    if (anyLeftGreater && anyRightGreater)
                        ThrowIncomparable(left, right);
                    return anyLeftGreater;
                default:
                    throw new NotSupportedException("Checkpoint tag mode is not supported in comparison");
            }
        }
        
        private static void ThrowIncomparable(CheckpointTag left, CheckpointTag right)
        {
            throw new InvalidOperationException(
                string.Format("Incomparable multi-stream checkpoint tags. '{0}' and '{1}'", left, right));
        }

        public static bool operator >=(CheckpointTag left, CheckpointTag right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (!ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;
            if (ReferenceEquals(left, null) && !ReferenceEquals(right, null))
                return false;
            var leftMode = left.Mode_;
            var rightMode = right.Mode_;
            UpgradeModes(ref leftMode, ref rightMode);
            if (leftMode != rightMode)
                throw new NotSupportedException("Cannot compare checkpoint tags in different modes");
            switch (leftMode)
            {
                case Mode.Position:
                    return left.Position >= right.Position;
                case Mode.PreparePosition:
                    return left.PreparePosition >= right.PreparePosition;
                case Mode.Stream:
                    if (left.Streams.Keys.First() != right.Streams.Keys.First())
                        throw new InvalidOperationException("Cannot compare checkpoint tags across different streams");
                    var result = left.Streams.Values.First() >= right.Streams.Values.First();
                    return result;
                case Mode.MultiStream:
                    int rvalue;
                    bool anyLeftGreater = left.Streams.Any(l => !right.Streams.TryGetValue(l.Key, out rvalue) || l.Value > rvalue);

                    int lvalue;
                    bool anyRightGreater = right.Streams.Any(r => !left.Streams.TryGetValue(r.Key, out lvalue) || r.Value > lvalue);

                    if (anyLeftGreater && anyRightGreater)
                        ThrowIncomparable(left, right);
                    return !anyRightGreater;
                default:
                    throw new NotSupportedException("Checkpoint tag mode is not supported in comparison");
            }
        }

        public static bool operator <(CheckpointTag left, CheckpointTag right)
        {
            return !(left >= right);
        }

        public static bool operator <=(CheckpointTag left, CheckpointTag right)
        {
            return !(left > right);
        }

        public static bool operator ==(CheckpointTag left, CheckpointTag right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CheckpointTag left, CheckpointTag right)
        {
            return !(left == right);
        }

        protected bool Equals(CheckpointTag other)
        {
            var leftMode = Mode_;
            var rightMode = other.Mode_;
            if (leftMode != rightMode)
                return false;
            UpgradeModes(ref leftMode, ref rightMode);
            switch (leftMode)
            {
                case Mode.Position:
                    return Position == other.Position;
                case Mode.PreparePosition:
                    return PreparePosition == other.PreparePosition;
                case Mode.Stream:
                    if (Streams.Keys.First() != other.Streams.Keys.First())
                        return false;
                    var result = Streams.Values.First() == other.Streams.Values.First();
                    return result;
                case Mode.MultiStream:
                    int rvalue;
                    return Streams.Count == other.Streams.Count
                           && Streams.All(l => other.Streams.TryGetValue(l.Key, out rvalue) && l.Value == rvalue);
                default:
                    throw new NotSupportedException("Checkpoint tag mode is not supported in comparison");
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CheckpointTag) obj);
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }


        public long? CommitPosition
        {
            get
            {
                return Streams != null
                           ? null
                           : (Position.CommitPosition != Int64.MinValue ? Position.CommitPosition : (long?) null);
            }
        }

        public long? PreparePosition
        {
            get
            {
                return Streams != null
                         ? null
                         : (Position.PreparePosition != Int64.MinValue ? Position.PreparePosition : (long?)null);
            }
        }

        internal readonly Mode Mode_;
        private static readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static CheckpointTag FromPosition(long commitPosition, long preparePosition)
        {
            return new CheckpointTag(new EventPosition(commitPosition, preparePosition));
        }

        public static CheckpointTag FromPosition(EventPosition position)
        {
            return new CheckpointTag(position);
        }

        public static CheckpointTag FromPreparePosition(long preparePosition)
        {
            return new CheckpointTag(preparePosition);
        }

        public static CheckpointTag FromStreamPosition(string stream, int sequenceNumber)
        {
            return new CheckpointTag(stream, sequenceNumber);
        }

        public static CheckpointTag FromStreamPositions(IDictionary<string, int> streams)
        {
            // clone to avoid changes to the dictionary passed as argument
            return new CheckpointTag(streams.ToDictionary(v => v.Key, v => v.Value));
        }

        public int CompareTo(CheckpointTag other)
        {
            return this < other ? -1 : (this > other ? 1 : 0);
        }

        public override string ToString()
        {
            switch (Mode_)
            {
                case Mode.Position:
                    return Position.ToString();
                case Mode.PreparePosition:
                    return PreparePosition.ToString();
                case Mode.Stream:
                    return Streams.Keys.First() + ": " + Streams.Values.First();
                case Mode.MultiStream:
                    var sb = new StringBuilder();
                    foreach (var stream in Streams)
                    {
                        sb.AppendFormat("{0}: {1}; ", stream.Key, stream.Value);
                    }
                    return sb.ToString();
                default:
                    return "Unsupported mode: " + base.ToString();
            }
        }

        private static void UpgradeModes(ref Mode leftMode, ref Mode rightMode)
        {
            if (leftMode == Mode.Stream && rightMode == Mode.MultiStream)
            {
                leftMode = Mode.MultiStream;
                return;
            }
            if (leftMode == Mode.MultiStream && rightMode == Mode.Stream)
            {
                rightMode = Mode.MultiStream;
                return;
            }
        }

        public CheckpointTag UpdateStreamPosition(string streamId, int eventSequenceNumber)
        {
            if (Mode_ != Mode.MultiStream)
                throw new ArgumentException("Invalid tag mode", "tag");
            var resultDictionary = new Dictionary<string, int>();
            foreach (var stream in Streams)
            {
                if (stream.Key == streamId)
                {
                    if (eventSequenceNumber < stream.Value)
                        throw new InvalidOperationException(
                            string.Format(
                                "Cannot make a checkpoint tag before the current position. Stream: '{0}'  Current: {1} Message Position Event SequenceNo: {2}",
                                stream.Key, stream.Value, eventSequenceNumber));
                    resultDictionary.Add(stream.Key, eventSequenceNumber);
                }
                else
                {
                    resultDictionary.Add(stream.Key, stream.Value);
                }
            }
            if (resultDictionary.Count < Streams.Count)
                resultDictionary.Add(streamId, eventSequenceNumber);
            return FromStreamPositions(resultDictionary);
        }

        public byte[] ToJsonBytes(ProjectionVersion projectionVersion, IEnumerable<KeyValuePair<string, string>> extraMetaData = null)
        {
            if (projectionVersion.ProjectionId <= 0) throw new ArgumentException("projectionId is required", "projectionVersion");

            using (var memoryStream = new MemoryStream())
            {
                using (var textWriter = new StreamWriter(memoryStream, _utf8NoBom))
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    WriteTo(projectionVersion, extraMetaData, jsonWriter);
                }
                return memoryStream.ToArray();
            }
        }

        public string ToJsonString(ProjectionVersion projectionVersion, IEnumerable<KeyValuePair<string, string>> extraMetaData = null)
        {
            if (projectionVersion.ProjectionId <= 0) throw new ArgumentException("projectionId is required", "projectionVersion");

            using (var textWriter = new StringWriter())
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    WriteTo(projectionVersion, extraMetaData, jsonWriter);
                }
                return textWriter.ToString();
            }
        }

        public string ToJsonString(IEnumerable<KeyValuePair<string, string>> extraMetaData = null)
        {
            using (var textWriter = new StringWriter())
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    WriteTo(default(ProjectionVersion), extraMetaData, jsonWriter);
                }
                return textWriter.ToString();
            }
        }

        public JRaw ToJsonRaw(IEnumerable<KeyValuePair<string, string>> extraMetaData = null)
        {
            using (var textWriter = new StringWriter())
            {
                using (var jsonWriter = new JsonTextWriter(textWriter))
                {
                    WriteTo(default(ProjectionVersion), extraMetaData, jsonWriter);
                }
                return new JRaw(textWriter.ToString());
            }
        }

        private void WriteTo(ProjectionVersion projectionVersion, IEnumerable<KeyValuePair<string, string>> extraMetaData, JsonTextWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            if (projectionVersion.ProjectionId > 0)
            {
                jsonWriter.WritePropertyName("$v");
                WriteVersion(projectionVersion, jsonWriter);
            }
            switch (Mode_)
            {
                case Mode.Position:
                    jsonWriter.WritePropertyName("$c");
                    jsonWriter.WriteValue(CommitPosition.GetValueOrDefault());
                    jsonWriter.WritePropertyName("$p");
                    jsonWriter.WriteValue(PreparePosition.GetValueOrDefault());
                    break;
                case Mode.PreparePosition:
                    jsonWriter.WritePropertyName("$p");
                    jsonWriter.WriteValue(PreparePosition.GetValueOrDefault());
                    break;
                case Mode.Stream:
                case Mode.MultiStream:
                    jsonWriter.WritePropertyName("$s");
                    jsonWriter.WriteStartObject();
                    foreach (var stream in Streams)
                    {
                        jsonWriter.WritePropertyName(stream.Key);
                        jsonWriter.WriteValue(stream.Value);
                    }
                    jsonWriter.WriteEndObject();
                    break;
            }
            if (extraMetaData != null)
            {
                foreach (var pair in extraMetaData)
                {
                    jsonWriter.WritePropertyName(pair.Key);
                    jsonWriter.WriteRawValue(pair.Value);
                }
            }
            jsonWriter.WriteEndObject();
        }

        private static void WriteVersion(ProjectionVersion projectionVersion, JsonTextWriter jsonWriter)
        {
            jsonWriter.WriteValue(projectionVersion.ProjectionId + ":" + projectionVersion.Epoch + ":" + projectionVersion.Version);
        }

        public static CheckpointTagVersion FromJson(JsonReader reader, ProjectionVersion current)
        {
            Check(reader.Read(), reader);
            Check(JsonToken.StartObject, reader);
            long? commitPosition = null;
            long? preparePosition = null;
            Dictionary<string, int> streams = null;
            Dictionary<string, JToken> extra = null;
            var projectionId = current.ProjectionId;
            var projectionEpoch = 0;
            var projectionVersion = 0;
            while (true)
            {
                Check(reader.Read(), reader);
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                Check(JsonToken.PropertyName, reader);
                var name = (string) reader.Value;
                switch (name)
                {
                    case "$v":
                    case "v":
                        Check(reader.Read(), reader);
                        if (reader.ValueType == typeof (long))
                        {
                            var v = (int)(long)reader.Value;
                            if (v > 0) // TODO: remove this if with time
                                projectionVersion = v;
                        }
                        else
                        {
                            //TODO: better handle errors
                            var v = (string) reader.Value;
                            string[] parts = v.Split(':');
                            if (parts.Length == 2)
                            {
                                projectionVersion = Int32.Parse(parts[1]);
                            }
                            else
                            {
                                projectionId = Int32.Parse(parts[0]);
                                projectionEpoch = Int32.Parse(parts[1]);
                                projectionVersion = Int32.Parse(parts[2]);
                            }
                        }
                        break;
                    case "$c":
                    case "c":
                    case "commitPosition":
                        Check(reader.Read(), reader);
                        commitPosition = (long) reader.Value;
                        break;
                    case "$p":
                    case "p":
                    case "preparePosition":
                        Check(reader.Read(), reader);
                        preparePosition = (long) reader.Value;
                        break;
                    case "$s":
                    case "s":
                    case "streams":
                        Check(reader.Read(), reader);
                        Check(JsonToken.StartObject, reader);
                        streams = new Dictionary<string, int>();
                        while (true)
                        {
                            Check(reader.Read(), reader);
                            if (reader.TokenType == JsonToken.EndObject)
                                break;
                            Check(JsonToken.PropertyName, reader);
                            var streamName = (string) reader.Value;
                            Check(reader.Read(), reader);
                            var position = (int)(long) reader.Value;
                            streams.Add(streamName, position);
                        }
                        break;
                    default:
                        if (extra == null)
                            extra = new Dictionary<string, JToken>();
                        Check(reader.Read(), reader);
                        var jToken = JToken.ReadFrom(reader);
                        extra.Add(name, jToken);
                        break;
                }
            }
            return new CheckpointTagVersion
                {
                    Tag =
                        new CheckpointTag(
                            new EventPosition(commitPosition ?? Int64.MinValue, preparePosition ?? Int64.MinValue), streams),
                    Version = new ProjectionVersion(projectionId, projectionEpoch, projectionVersion),
                    ExtraMetadata = extra,
                };
        }

        private static void Check(JsonToken type, JsonReader reader)
        {
            if (reader.TokenType != type)
                throw new Exception("Invalid JSON");
        } 

        private static void Check(bool read, JsonReader reader)
        {
            if (!read)
                throw new Exception("Invalid JSON");
        }
    }
}
