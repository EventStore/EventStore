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
using EventStore.Core.Data;

namespace EventStore.Projections.Core
{
    public struct EventPosition : IComparable<EventPosition>, IEquatable<EventPosition>
    {
        private readonly long _commitPosition;
        private readonly long _preparePosition;

        public EventPosition(long commitPosition, long preparePosition)
            : this()
        {
            // do not compare - required to deserialize in any order
            _commitPosition = commitPosition;
            _preparePosition = preparePosition;
        }

        public static implicit operator EventPosition(TFPos pos)
        {
            return new EventPosition(pos.CommitPosition, pos.PreparePosition);
        }

        public long CommitPosition
        {
            get { return _commitPosition; }
        }

        public long PreparePosition
        {
            get { return _preparePosition; }
        }

        public int CompareTo(EventPosition other)
        {
            if (_commitPosition < other._commitPosition)
                return -1;
            if (_commitPosition > other._commitPosition)
                return 1;
            return _preparePosition.CompareTo(other._preparePosition);
        }

        public bool Equals(EventPosition other)
        {
            return _commitPosition == other._commitPosition && _preparePosition == other._preparePosition;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is EventPosition && Equals((EventPosition) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_commitPosition.GetHashCode()*397) ^ _preparePosition.GetHashCode();
            }
        }

        public static bool operator ==(EventPosition d1, EventPosition d2)
        {
            return d1.Equals(d2);
        }

        public static bool operator !=(EventPosition d1, EventPosition d2)
        {
            return !d1.Equals(d2);
        }

        public static bool operator <(EventPosition t1, EventPosition t2)
        {
            return t1.CompareTo(t2) < 0;
        }

        public static bool operator <=(EventPosition t1, EventPosition t2)
        {
            return t1.CompareTo(t2) <= 0;
        }

        public static bool operator >(EventPosition t1, EventPosition t2)
        {
            return t1.CompareTo(t2) > 0;
        }

        public static bool operator >=(EventPosition t1, EventPosition t2)
        {
            return t1.CompareTo(t2) >= 0;
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", _commitPosition, _preparePosition);
        }
    }
}
