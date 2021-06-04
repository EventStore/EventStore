using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;
using Serilog;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class MemoryMappedFileBloomFilter {
		public const long MinSizeKB = 10;
		public const long MaxSizeKB = 4_000_000;
	}

	public abstract class MemoryMappedFileBloomFilter<TItem> : MemoryMappedFileBloomFilter, IProbabilisticFilter<TItem>, IDisposable {
		/*
		    Bloom filter implementation based on the following paper by Adam Kirsch and Michael Mitzenmacher:
		    "Less Hashing, Same Performance: Building a Better Bloom Filter"
		    https://www.eecs.harvard.edu/~michaelm/postscripts/rsa2008.pdf

		    Only two 32-bit hash functions can be used to simulate additional hash functions of the form g(x) = h1(x) + i*h2(x)
		*/
		private const int NumHashFunctions = 6;
		private readonly double _falsePositiveProbability = Math.Pow(2, -NumHashFunctions); //approximately 0.02
		public double FalsePositiveProbability => _falsePositiveProbability;
		private readonly long _numBits;
		public readonly long OptimalMaxItems;

		private readonly IHasher[] _hashers = {
			new XXHashUnsafe(seed: 0xC0015EEDU),
			new Murmur3AUnsafe(seed: 0xC0015EEDU),
			new XXHashUnsafe(seed: 0x13375EEDU),
			new Murmur3AUnsafe(seed: 0x13375EEDU)
		};
		private readonly MemoryMappedFile _mmf;
		private readonly MemoryMappedViewAccessor _mmfDataReadAccessor;
		private readonly MemoryMappedViewAccessor _mmfDataWriteAccessor;

		/// <summary>
		/// Bloom filter implementation which uses a memory-mapped file for persistence.
		/// </summary>
		/// <param name="path">Path to the bloom filter file</param>
		/// <param name="size">Size of the bloom filter in bytes</param>
		/// <param name="serializer">Function to serialize an item to a byte array</param>
		public MemoryMappedFileBloomFilter(string path, long size) {
			Ensure.NotNull(path, nameof(path));

			if (size < MinSizeKB * 1000 || size > MaxSizeKB * 1000) {
				throw new ArgumentOutOfRangeException(nameof(size), $"size should be between {MinSizeKB:N0} and {MaxSizeKB:N0} KB inclusive");
			}

			_numBits = size * 8;
			OptimalMaxItems = Convert.ToInt64(-_numBits * Math.Log(2) * Math.Log(2) / Math.Log(_falsePositiveProbability));

			var newFile = !File.Exists(path);
			if (newFile) {
				_mmf = MemoryMappedFile.CreateFromFile(path, FileMode.CreateNew, null, Header.Size + _numBits / 8, MemoryMappedFileAccess.ReadWrite);
				Header header = new() {
					Version = Header.CurrentVersion,
					NumBits = _numBits
				};
				header.WriteTo(_mmf);
			} else {
				_mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
				try {
					var header = Header.ReadFrom(_mmf);
					if (header.NumBits != _numBits) {
						throw new CorruptedFileException(
							$"Calculated number of bits ({_numBits}) does not match with number of bits in file ({header.NumBits}).");
					}
				} catch (CorruptedFileException) {
					_mmf?.Dispose();
					throw;
				}
			}

			_mmfDataReadAccessor = _mmf.CreateViewAccessor(Header.Size, 0, MemoryMappedFileAccess.Read);
			_mmfDataWriteAccessor = _mmf.CreateViewAccessor(Header.Size, 0, MemoryMappedFileAccess.Write);
		}

		protected abstract ReadOnlySpan<byte> Serialize(TItem item);

		public void Add(TItem item) {
			foreach(var bitPosition in GetBitPositions(item)) {
				SetBit(bitPosition);
			}
		}

		public bool MayExist(TItem item) {
			foreach(var bitPosition in GetBitPositions(item)) {
				if (!IsBitSet(bitPosition)) return false;
			}
			return true;
		}

		public void Flush() {
			_mmfDataWriteAccessor.Flush();
		}

		private IEnumerable<long> GetBitPositions(TItem item) {
			var bytes = Serialize(item);
			long hash1 = ((long)_hashers[0].Hash(bytes) << 32) | _hashers[1].Hash(bytes);
			long hash2 = ((long)_hashers[2].Hash(bytes) << 32) | _hashers[3].Hash(bytes);

			long hash = hash1;
			for (int i = 0; i < NumHashFunctions; i++) {
				hash += hash2;
				hash &= long.MaxValue; //make non-negative
				long bitPosition = hash % _numBits;
				yield return bitPosition;
			}
		}

		private void SetBit(long position) {
			var bytePosition = position / 8;
			_mmfDataReadAccessor.Read(bytePosition, out byte byteValue);
			byteValue = (byte) (byteValue | (1 << (int)(7 - position % 8)));
			_mmfDataWriteAccessor.Write(bytePosition, byteValue);
		}

		private bool IsBitSet(long position) {
			var bytePosition = position / 8;
			_mmfDataReadAccessor.Read(bytePosition, out byte byteValue);
			return (byteValue & (1 << (int)(7 - position % 8))) != 0;
		}

		public void Dispose() {
			_mmfDataReadAccessor?.Dispose();
			_mmfDataWriteAccessor?.Dispose();
			_mmf?.Dispose();
		}
	}
}
