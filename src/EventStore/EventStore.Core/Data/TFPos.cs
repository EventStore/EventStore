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
using System.Diagnostics.Contracts;

namespace EventStore.Core.Data
{
    public struct TFPos : IEquatable<TFPos>, IComparable<TFPos>
    {
        public static readonly TFPos Invalid = new TFPos(-1, -1);
        public static readonly TFPos HeadOfTf = new TFPos(-1, -1);

        public readonly long CommitPosition;
        public readonly long PreparePosition;

        public TFPos(long commitPosition, long preparePosition)
        {
            CommitPosition = commitPosition;
            PreparePosition = preparePosition;
        }

        [Pure]
        public string AsString()
        {
            return string.Format("{0:X16}{1:X16}", CommitPosition, PreparePosition);
        }

        public static bool TryParse(string s, out TFPos pos)
        {
            pos = Invalid;
            if (s == null || s.Length != 32)
                return false;

            long commitPos;
            long preparePos;
            if (!long.TryParse(s.Substring(0, 16), System.Globalization.NumberStyles.HexNumber, null, out commitPos))
                return false;
            if (!long.TryParse(s.Substring(16, 16), System.Globalization.NumberStyles.HexNumber, null, out preparePos))
                return false;
            pos = new TFPos(commitPos, preparePos);
            return true;
        }

        public int CompareTo(TFPos other)
        {
            if (CommitPosition < other.CommitPosition)
                return -1;
            if (CommitPosition > other.CommitPosition)
                return 1;
            return PreparePosition.CompareTo(other.PreparePosition);
        }

        public bool Equals(TFPos other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TFPos && Equals((TFPos) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (CommitPosition.GetHashCode()*397) ^ PreparePosition.GetHashCode();
            }
        }

        public static bool operator ==(TFPos left, TFPos right)
        {
            return left.CommitPosition == right.CommitPosition && left.PreparePosition == right.PreparePosition;
        }

        public static bool operator !=(TFPos left, TFPos right)
        {
            return !(left == right);
        }

        public static bool operator <=(TFPos left, TFPos right)
        {
            return !(left > right);
        }

        public static bool operator >=(TFPos left, TFPos right)
        {
            return !(left < right);
        }

        public static bool operator <(TFPos left, TFPos right)
        {
            return left.CommitPosition < right.CommitPosition
                   || (left.CommitPosition == right.CommitPosition && left.PreparePosition < right.PreparePosition);
        }

        public static bool operator >(TFPos left, TFPos right)
        {
            return left.CommitPosition > right.CommitPosition
                   || (left.CommitPosition == right.CommitPosition && left.PreparePosition > right.PreparePosition);
        }

        public override string ToString()
        {
            return string.Format("C:{0}/P:{1}", CommitPosition, PreparePosition);
        }
    }
}