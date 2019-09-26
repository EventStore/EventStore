using System;
using System.Linq;

namespace EventStore.Grpc {
#if EVENTSTORE_GRPC_PUBLIC
	public
#else
	internal
#endif
		struct Uuid : IEquatable<Uuid> {
		public static readonly Uuid Empty = new Uuid(new byte[16]);
		private readonly byte[] _value;

		public Uuid(ReadOnlySpan<byte> value) : this(value.ToArray()) {
		}

		public Uuid(byte[] value) {
			if (value == null) {
				throw new ArgumentNullException(nameof(value));
			}

			if (value.Length != 16) {
				throw new ArgumentOutOfRangeException(nameof(value));
			}

			_value = value;
		}

		public readonly bool Equals(Uuid other) => _value.SequenceEqual(other._value);
		public override readonly bool Equals(object obj) => obj is Uuid other && Equals(other);
		public override readonly int GetHashCode() => HashCode.Hash.Combine(_value);
		public static bool operator ==(Uuid left, Uuid right) => left.Equals(right);
		public static bool operator !=(Uuid left, Uuid right) => !left.Equals(right);
		public override readonly string ToString() => string.Join(string.Empty, _value.Select(x => x.ToString("x2")));
		public readonly ReadOnlySpan<byte> ToSpan() => _value;

		public readonly Guid ToGuid() {
			if (!BitConverter.IsLittleEndian) {
				return new Guid(_value);
			}

			var result = new byte[16];
			Array.Copy(_value, 0, result, 0, 16);
			Array.Reverse(result, 0, 4);
			Array.Reverse(result, 4, 2);
			Array.Reverse(result, 6, 2);

			return new Guid(result);
		}

		public static Uuid FromGuid(Guid value) {
			var result = value.ToByteArray();
			if (!BitConverter.IsLittleEndian) {
				return new Uuid(result);
			}

			Array.Reverse(result, 0, 4);
			Array.Reverse(result, 4, 2);
			Array.Reverse(result, 6, 2);

			return new Uuid(result);
		}

		public static Uuid NewUuid() => FromGuid(Guid.NewGuid());
	}
}
