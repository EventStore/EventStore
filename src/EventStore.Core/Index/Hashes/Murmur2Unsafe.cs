using System;

namespace EventStore.Core.Index.Hashes {
	public class Murmur2Unsafe : IHasher {
		private const uint Seed = 0xc58f1a7b;

		private const UInt32 m = 0x5bd1e995;
		private const Int32 r = 24;

		public unsafe UInt32 Hash(string s) {
			fixed (char* input = s) {
				return Hash((byte*)input, (uint)s.Length * sizeof(char), Seed);
			}
		}

		public unsafe uint Hash(byte[] data) {
			fixed (byte* input = &data[0]) {
				return Hash(input, (uint)data.Length, Seed);
			}
		}

		public unsafe uint Hash(byte[] data, int offset, uint len, uint seed) {
			fixed (byte* input = &data[offset]) {
				return Hash(input, len, seed);
			}
		}

		private unsafe static uint Hash(byte* data, uint len, uint seed) {
			UInt32 h = seed ^ len;
			UInt32 numberOfLoops = len >> 2; // div 4

			UInt32* realData = (UInt32*)data;
			while (numberOfLoops > 0) {
				UInt32 k = *realData;

				k *= m;
				k ^= k >> r;
				k *= m;

				h *= m;
				h ^= k;

				realData++;
				numberOfLoops--;
			}

			var tail = (byte*)realData;
			switch (len & 3) // mod 4
			{
				case 3:
					h ^= (uint)(tail[2] << 16);
					h ^= (uint)(tail[1] << 8);
					h ^= tail[0];
					h *= m;
					break;
				case 2:
					h ^= (uint)(tail[1] << 8);
					h ^= tail[0];
					h *= m;
					break;
				case 1:
					h ^= tail[0];
					h *= m;
					break;
			}

			// Do a few final mixes of the hash to ensure the last few
			// bytes are well-incorporated.

			h ^= h >> 13;
			h *= m;
			h ^= h >> 15;

			return h;
		}
	}
}
