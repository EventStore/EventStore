using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Index.Hashers {
	public class ConstantHasher : IHasher, IHasher<string> {
		private readonly uint _const;
		public ConstantHasher(uint @const) {
			_const = @const;
		}

		public uint Hash(string s) {
			return _const;
		}

		public uint Hash(byte[] data) {
			return _const;
		}

		public uint Hash(byte[] data, int offset, uint len, uint seed) {
			return _const;
		}
	}
}
