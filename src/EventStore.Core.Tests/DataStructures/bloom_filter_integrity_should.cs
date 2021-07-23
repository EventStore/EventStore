using System;
using System.Linq;
using System.Runtime.InteropServices;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using EventStore.Core.Index.Hashes;
using NUnit.Framework;

namespace EventStore.Core.Tests.DataStructures {
	[TestFixture]
	public class byte_extensions_should {
		[DatapointSource]
		public static object[][] IsBitSetCases = new object[][] {
			new object[] { true, (byte)0b1111_0000, 0 },
			new object[] { true, (byte)0b1111_0000, 1 },
			new object[] { true, (byte)0b1111_0000, 2 },
			new object[] { true, (byte)0b1111_0000, 3 },
			new object[] { false, (byte)0b1111_0000, 4 },
			new object[] { false, (byte)0b1111_0000, 5 },
			new object[] { false, (byte)0b1111_0000, 6 },
			new object[] { false, (byte)0b1111_0000, 7 },
			new object[] { false, (byte)0b0000_1111, 0 },
			new object[] { false, (byte)0b0000_1111, 1 },
			new object[] { false, (byte)0b0000_1111, 2 },
			new object[] { false, (byte)0b0000_1111, 3 },
			new object[] { true, (byte)0b0000_1111, 4 },
			new object[] { true, (byte)0b0000_1111, 5 },
			new object[] { true, (byte)0b0000_1111, 6 },
			new object[] { true, (byte)0b0000_1111, 7 },
		};

		[TestCaseSource(nameof(IsBitSetCases))]
		public void check_bit_correctly(bool expected, byte x, int bitIndex) {
			Assert.AreEqual(expected, x.IsBitSet(bitIndex));
		}

		[DatapointSource]
		public static object[][] SetBitCases = new object[][] {
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 0 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 1 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 2 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 3 },
			new object[] { (byte)0b1111_1000, (byte)0b1111_0000, 4 },
			new object[] { (byte)0b1111_0100, (byte)0b1111_0000, 5 },
			new object[] { (byte)0b1111_0010, (byte)0b1111_0000, 6 },
			new object[] { (byte)0b1111_0001, (byte)0b1111_0000, 7 },
			new object[] { (byte)0b1000_1111, (byte)0b0000_1111, 0 },
			new object[] { (byte)0b0100_1111, (byte)0b0000_1111, 1 },
			new object[] { (byte)0b0010_1111, (byte)0b0000_1111, 2 },
			new object[] { (byte)0b0001_1111, (byte)0b0000_1111, 3 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 4 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 5 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 6 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 7 },
		};

		[TestCaseSource(nameof(SetBitCases))]
		public void set_bit_correctly(byte expected, byte x, int bitIndex) {
			Assert.AreEqual(expected, x.SetBit(bitIndex));
		}

		[DatapointSource]
		public static object[][] UnsetBitCases = new object[][] {
			new object[] { (byte)0b0111_0000, (byte)0b1111_0000, 0 },
			new object[] { (byte)0b1011_0000, (byte)0b1111_0000, 1 },
			new object[] { (byte)0b1101_0000, (byte)0b1111_0000, 2 },
			new object[] { (byte)0b1110_0000, (byte)0b1111_0000, 3 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 4 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 5 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 6 },
			new object[] { (byte)0b1111_0000, (byte)0b1111_0000, 7 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 0 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 1 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 2 },
			new object[] { (byte)0b0000_1111, (byte)0b0000_1111, 3 },
			new object[] { (byte)0b0000_0111, (byte)0b0000_1111, 4 },
			new object[] { (byte)0b0000_1011, (byte)0b0000_1111, 5 },
			new object[] { (byte)0b0000_1101, (byte)0b0000_1111, 6 },
			new object[] { (byte)0b0000_1110, (byte)0b0000_1111, 7 },
		};

		[TestCaseSource(nameof(UnsetBitCases))]
		public void unset_bit_correctly(byte expected, byte x, int bitIndex) {
			Assert.AreEqual(expected, x.UnsetBit(bitIndex));
		}
	}

	[TestFixture]
	public class bloom_filter_integrity_should {
		private static byte[] NewCacheLine(byte b) {
			var xs = new byte[64];
			for (int i = 0; i < xs.Length; i++) {
				xs[i] = b;
			}
			return xs;
		}

		[DatapointSource]
		public static object[][] RecoverCases = new object[][] {
			new object[] { "all zeros is valid", true, NewCacheLine(0x00) },
			new object[] { "all zeros with hash is valid", true, NewCacheLine(0x00).WithHash() },
			new object[] { "all ones is invalid", false, NewCacheLine(0xFF) },
			new object[] { "all ones with hash is valid", true, NewCacheLine(0xFF).WithHash() },
			new object[] { "data but no hash is invalid", false, NewCacheLine(0x00).With(3, 0xAB) },
			new object[] { "data with hash is valid", true, NewCacheLine(0x00).With(3, 0xAB).WithHash() },
		};

		[TestCaseSource(nameof(RecoverCases))]
		public void recover_correctly(string name, bool expected, byte[] bytes) {
			var originalBytes = bytes.ToArray();

			var byteSpan = bytes.AsSpan();

			if (expected) {
				Assert.True(BloomFilterIntegrity.ValidateHash(byteSpan));

				// bytes should be unchanged
				for (var i = 0; i < bytes.Length; i++) {
					Assert.AreEqual(originalBytes[i], bytes[i]);
				}
			} else {
				Assert.False(BloomFilterIntegrity.ValidateHash(byteSpan));

				// bytes should be all 1s
				foreach (var b in bytes) {
					Assert.AreEqual(0b1111_1111, b);
				}
			}
		}

		[DatapointSource]
		public static object[][] WriteCases = new object[][] {
			new object[] { "all 0x00", true, NewCacheLine(0x00) },
			new object[] { "all 0xAA", true, NewCacheLine(0xAA) },
			new object[] { "all 0xFF - corrupted and made safe", false, NewCacheLine(0xFF) },
		};

		[TestCaseSource(nameof(WriteCases))]
		public void write_correctly(string name, bool expected, byte[] bytes) {
			var originalBytes = bytes.ToArray();
			var byteSpan = bytes.AsSpan();

			BloomFilterIntegrity.WriteHash(byteSpan);

			// bytes before the hash should be unchanged
			for (var i = 0; i < bytes.Length - sizeof(uint); i++) {
				Assert.AreEqual(originalBytes[i], bytes[i]);
			}

			// cacheline should be valid
			if (expected) {
				Assert.True(BloomFilterIntegrity.ValidateHash(byteSpan));
			} else {
				Assert.False(BloomFilterIntegrity.ValidateHash(byteSpan));
			}
		}

		[TestCase]
		public void rehash_when_one_too_many_bits_are_set() {
			// some data with hash
			var cacheLine = NewCacheLine(0x01).WithHash();

			// set an extra bit
			cacheLine[3] = cacheLine[3].SetBit(3);
			var copy = cacheLine.ToArray();

			// still valid
			Assert.True(BloomFilterIntegrity.ValidateHash(cacheLine));

			// same data
			CollectionAssert.AreEqual(copy[..^4], cacheLine[..^4]);

			// changed hash
			CollectionAssert.AreNotEqual(copy[^4..], cacheLine[^4..]);
		}

		[TestCase]
		public void not_rehash_when_very_different() {
			// some data with hash
			var cacheLine = NewCacheLine(0x01).WithHash();

			// corrupt it
			cacheLine.With(0, 0xEE);

			// not valid
			Assert.False(BloomFilterIntegrity.ValidateHash(cacheLine));
		}
	}

	static class ByteArrayExtensions {
		public static byte[] With(this byte[] self, int offset, byte value) {
			self[offset] = value;
			return self;
		}

		public static byte[] WithHash(this byte[] self) {
			var hasher = new XXHashUnsafe();
			var hash = hasher.Hash(self.AsSpan()[..^4]);
			var hashSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref hash, 1));
			hashSpan.CopyTo(self.AsSpan()[^4..]);
			return self;
		}
	}
}
