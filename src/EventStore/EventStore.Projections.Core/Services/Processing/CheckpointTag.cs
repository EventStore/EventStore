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
using System.Runtime.Serialization;
using System.Linq;
using System.ServiceModel;

namespace EventStore.Projections.Core.Services.Processing
{

    [DataContract]
    public class CheckpointTag: IComparable<CheckpointTag>
    {
        private enum Mode
        {
            Position,
            Stream,
            MultiStream
        }

		internal CheckpointTag ()
		{
		}

        public CheckpointTag(EventPosition position)
        {
            Position = position;
        }

        public CheckpointTag(CheckpointTag simpleTag, Dictionary<string, int> streams)
        {
            Position = simpleTag.Position;
            Streams = new Dictionary<string, int>(streams); // clone
        }

        private CheckpointTag(string stream, int sequenceNumber, long prepaprePosition)
        {
            Position = new EventPosition(long.MinValue, prepaprePosition);
            Streams = new Dictionary<string, int> {{stream, sequenceNumber}};
        }

        private Mode GetMode()
        {
            if (Streams == null || Streams.Count == 0)
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
            var leftMode = left.GetMode();
            var rightMode = right.GetMode();
            if (leftMode != rightMode)
                throw new NotSupportedException("Cannot compare checkpoint tags in different modes");
            switch (leftMode)
            {
                case Mode.Position:
                    return left.Position > right.Position;
                case Mode.Stream:
                    if (left.Streams.Keys.First() != right.Streams.Keys.First())
                        throw new NotSupportedException("Cannot compare checkpoint tags across different streams");
                    var result = left.Streams.Values.First() > right.Streams.Values.First();
                    if (result != (left.Position.PreparePosition > right.Position.PreparePosition))
                        ThrowBadTags(left, right);
                    return result;
                default:
                    throw new NotSupportedException("Checkpoint tag mode is not supported in comparison");
            }
        }

        private static void ThrowBadTags(CheckpointTag left, CheckpointTag right)
        {
            throw new InvalidOperationException(
                string.Format(
                    "Invalid checkpoint tags detected. Comparison of sequence numbers and prepapre positions gives different results",
                    left, right));
        }

        public static bool operator >=(CheckpointTag left, CheckpointTag right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (!ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;
            if (ReferenceEquals(left, null) && !ReferenceEquals(right, null))
                return false;
            var leftMode = left.GetMode();
            var rightMode = right.GetMode();
            if (leftMode != rightMode)
                throw new NotSupportedException("Cannot compare checkpoint tags in different modes");
            switch (leftMode)
            {
                case Mode.Position:
                    return left.Position >= right.Position;
                case Mode.Stream:
                    if (left.Streams.Keys.First() != right.Streams.Keys.First())
                        throw new NotSupportedException("Cannot compare checkpoint tags across different streams");
                    var result = left.Streams.Values.First() >= right.Streams.Values.First();
                    if (result != (left.Position.PreparePosition >= right.Position.PreparePosition))
                        ThrowBadTags(left, right);
                    return result;
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
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null))
                return false;
            return Equals(left, right);
        }

        public static bool operator !=(CheckpointTag left, CheckpointTag right)
        {
            return !(left == right);
        }


        protected bool Equals(CheckpointTag other)
        {
            var leftMode = GetMode();
            var rightMode = other.GetMode();
            if (leftMode != rightMode)
                return false;
            switch (leftMode)
            {
                case Mode.Position:
                    return Position == other.Position;
                case Mode.Stream:
                    if (Streams.Keys.First() != other.Streams.Keys.First())
                        return false;
                    var result = Streams.Values.First() == other.Streams.Values.First();
                    if (result != (Position.PreparePosition == other.Position.PreparePosition))
                        ThrowBadTags(this, other);
                    return result;
                default:
                    throw new NotSupportedException("Checkpoint tag mode is not supported in comparison");
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CheckpointTag) obj);
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }

        public EventPosition Position { get; private set; }


        [DataMember]
        public long? CommitPosition
        {
            get { return Streams != null ? (long?) null : Position.CommitPosition; }
            set { Position = new EventPosition(value == null ? long.MinValue : value.GetValueOrDefault(), Position.PreparePosition); }
        }

        [DataMember]
        public long? PreparePosition
        {
            get { return Position.PreparePosition != long.MinValue ? Position.PreparePosition: (long?)null; }
            set { Position = new EventPosition(Position.CommitPosition, value == null ? long.MinValue : value.GetValueOrDefault()); }
        }

        [DataMember]
        public Dictionary<string, int> Streams { get; private set; }

        public static CheckpointTag FromPosition(long commitPosition, long preparePosition)
        {
            return new CheckpointTag(new EventPosition(commitPosition, preparePosition));
        }

        public static CheckpointTag FromStreamPosition(string stream, int sequenceNumber, long prepaprePosition)
        {
            return new CheckpointTag(stream, sequenceNumber, prepaprePosition);
        }

        public int CompareTo(CheckpointTag other)
        {
            return this < other ? -1 : (this > other ? 1 : 0);
        }

        public override string ToString()
        {
            switch (GetMode())
            {
                case Mode.Position:
                    return Position.ToString();
                case Mode.Stream:
                    return Streams.Keys.First() + ": " + Streams.Values.First() + "(" + Position.PreparePosition + ")";
                default:
                    return "Unsupported mode: " + base.ToString();
            }
        }
    }
}
