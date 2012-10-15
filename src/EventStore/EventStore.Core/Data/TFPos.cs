using System;

namespace EventStore.Core.Data
{
    public struct TFPos : IEquatable<TFPos>
    {
        public static readonly TFPos Invalid = new TFPos(-1, -1);

        public readonly long CommitPosition;
        public readonly long PreparePosition;

        public TFPos(long commitPosition, long preparePosition)
        {
            CommitPosition = commitPosition;
            PreparePosition = preparePosition;
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