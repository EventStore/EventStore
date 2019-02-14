using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A structure referring to a potential logical record position
	/// in the Event Store transaction file.
	/// </summary>
	public struct Position {
		/// <summary>
		/// Position representing the start of the transaction file
		/// </summary>
		public static readonly Position Start = new Position(0, 0);

		/// <summary>
		/// Position representing the end of the transaction file
		/// </summary>
		public static readonly Position End = new Position(-1, -1);

		/// <summary>
		/// The commit position of the record
		/// </summary>
		public readonly long CommitPosition;

		/// <summary>
		/// The prepare position of the record.
		/// </summary>
		public readonly long PreparePosition;

		/// <summary>
		/// Constructs a position with the given commit and prepare positions.
		/// It is not guaranteed that the position is actually the start of a
		/// record in the transaction file.
		/// 
		/// The commit position cannot be less than the prepare position.
		/// </summary>
		/// <param name="commitPosition">The commit position of the record.</param>
		/// <param name="preparePosition">The prepare position of the record.</param>
		public Position(long commitPosition, long preparePosition) {
			if (commitPosition < preparePosition)
				throw new ArgumentException("The commit position cannot be less than the prepare position",
					"commitPosition");

			CommitPosition = commitPosition;
			PreparePosition = preparePosition;
		}

		/// <summary>
		/// Compares whether p1 &lt; p2.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 &lt; p2.</returns>
		public static bool operator <(Position p1, Position p2) {
			return p1.CommitPosition < p2.CommitPosition ||
			       (p1.CommitPosition == p2.CommitPosition && p1.PreparePosition < p2.PreparePosition);
		}


		/// <summary>
		/// Compares whether p1 &gt; p2.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 &gt; p2.</returns>
		public static bool operator >(Position p1, Position p2) {
			return p1.CommitPosition > p2.CommitPosition ||
			       (p1.CommitPosition == p2.CommitPosition && p1.PreparePosition > p2.PreparePosition);
		}

		/// <summary>
		/// Compares whether p1 &gt;= p2.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 &gt;= p2.</returns>
		public static bool operator >=(Position p1, Position p2) {
			return p1 > p2 || p1 == p2;
		}

		/// <summary>
		/// Compares whether p1 &lt;= p2.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 &lt;= p2.</returns>
		public static bool operator <=(Position p1, Position p2) {
			return p1 < p2 || p1 == p2;
		}

		/// <summary>
		/// Compares p1 and p2 for equality.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 is equal to p2.</returns>
		public static bool operator ==(Position p1, Position p2) {
			return p1.CommitPosition == p2.CommitPosition && p1.PreparePosition == p2.PreparePosition;
		}

		/// <summary>
		/// Compares p1 and p2 for equality.
		/// </summary>
		/// <param name="p1">A <see cref="Position" />.</param>
		/// <param name="p2">A <see cref="Position" />.</param>
		/// <returns>True if p1 is not equal to p2.</returns>
		public static bool operator !=(Position p1, Position p2) {
			return !(p1 == p2);
		}

		/// <summary>
		/// Indicates whether this instance and a specified object are equal.
		/// </summary>
		/// <returns>
		/// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
		/// </returns>
		/// <param name="obj">Another object to compare to. </param><filterpriority>2</filterpriority>
		public override bool Equals(object obj) {
			return obj is Position && Equals((Position)obj);
		}

		/// <summary>
		/// Compares this instance of <see cref="Position" /> for equality
		/// with another instance.
		/// </summary>
		/// <param name="other">A <see cref="Position" /></param>
		/// <returns>True if this instance is equal to the other instance.</returns>
		public bool Equals(Position other) {
			return this == other;
		}

		/// <summary>
		/// Returns the hash code for this instance.
		/// </summary>
		/// <returns>
		/// A 32-bit signed integer that is the hash code for this instance.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override int GetHashCode() {
			unchecked {
				return (CommitPosition.GetHashCode() * 397) ^ PreparePosition.GetHashCode();
			}
		}

		/// <summary>
		/// Returns the fully qualified type name of this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> containing a fully qualified type name.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override string ToString() {
			return string.Format("{0}/{1}", CommitPosition, PreparePosition);
		}
	}
}
