// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Index.Hashes;

public class Murmur3AUnsafe(uint seed = 0xc58f1a7b) : IHasher, IHasher<string> {
	private const uint c1 = 0xcc9e2d51;
	private const uint c2 = 0x1b873593;

	public unsafe uint Hash(string s) {
		fixed (char* input = s) {
			return Hash((byte*)input, (uint)s.Length * sizeof(char), seed);
		}
	}

	public unsafe uint Hash(ReadOnlySpan<byte> data) {
		fixed (byte* input = data) {
			return Hash(input, (uint)data.Length, seed);
		}
	}

	public unsafe uint Hash(byte[] data, int offset, uint len, uint ls) {
		fixed (byte* input = &data[offset]) {
			return Hash(input, len, ls);
		}
	}

	static unsafe uint Hash(byte* data, uint len, uint ls) {
		uint nblocks = len / 4;
		uint h1 = ls;

		// body
		uint k1;
		uint* block = (uint*)data;
		for (uint i = nblocks; i > 0; --i, ++block) {
			k1 = *block;

			k1 *= c1;
			k1 = Rotl32(k1, 15);
			k1 *= c2;

			h1 ^= k1;
			h1 = Rotl32(h1, 13);
			h1 = h1 * 5 + 0xe6546b64;
		}

		//----------
		// tail
		k1 = 0;
		uint rem = len & 3;
		byte* tail = (byte*)block;
		if (rem >= 3)
			k1 ^= (uint)(tail[2] << 16);
		if (rem >= 2)
			k1 ^= (uint)(tail[1] << 8);
		if (rem > 0) {
			k1 ^= tail[0];
			k1 *= c1;
			k1 = Rotl32(k1, 15);
			k1 *= c2;
			h1 ^= k1;
		}

		//----------
		// finalization

		h1 ^= len;

		h1 ^= h1 >> 16;
		h1 *= 0x85ebca6b;
		h1 ^= h1 >> 13;
		h1 *= 0xc2b2ae35;
		h1 ^= h1 >> 16;

		return h1;
	}

	private static uint Rotl32(uint x, int r) {
		return (x << r) | (x >> (32 - r));
	}
}
