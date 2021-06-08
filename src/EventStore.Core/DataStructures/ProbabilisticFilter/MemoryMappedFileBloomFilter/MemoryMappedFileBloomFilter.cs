using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public class MemoryMappedFileBloomFilter : IProbabilisticFilter, IDisposable {
		/*
		    Bloom filter implementation based on the following paper by Adam Kirsch and Michael Mitzenmacher:
		    "Less Hashing, Same Performance: Building a Better Bloom Filter"
		    https://www.eecs.harvard.edu/~michaelm/postscripts/rsa2008.pdf

		    Only two 32-bit hash functions can be used to simulate additional hash functions of the form g(x) = h1(x) + i*h2(x)
		*/
		public const long MinSizeKB = 10;
		public const long MaxSizeKB = 4_000_000;
		public const double RecommendedFalsePositiveProbability = 0.02;
		private const int NumHashFunctions = 6;
		private readonly long _numBits;

		private readonly IHasher[] _hashers = {
			new XXHashUnsafe(seed: 0xC0015EEDU),
			new Murmur3AUnsafe(seed: 0xC0015EEDU),
			new XXHashUnsafe(seed: 0x13375EEDU),
			new Murmur3AUnsafe(seed: 0x13375EEDU)
		};
		private readonly MemoryMappedFile _mmf;
		private readonly ObjectPool<MemoryMappedViewAccessor> _mmfReadersPool;
		private readonly MemoryMappedViewAccessor _mmfWriteAccessor;
		private readonly ReaderWriterLockSlim _readerWriterLock;

		/// <summary>
		/// Bloom filter implementation which uses a memory-mapped file for persistence.
		/// </summary>
		/// <param name="path">Path to the bloom filter file</param>
		/// <param name="size">Size of the bloom filter in bytes</param>
		/// <param name="serializer">Function to serialize an item to a byte array</param>
		public MemoryMappedFileBloomFilter(string path, long size, int initialReaderCount, int maxReaderCount) {
			Ensure.NotNull(path, nameof(path));

			if (size < MinSizeKB * 1000 || size > MaxSizeKB * 1000) {
				throw new ArgumentOutOfRangeException(nameof(size), $"size should be between {MinSizeKB:N0} and {MaxSizeKB:N0} KB inclusive");
			}

			_numBits = size * 8;

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

			_mmfReadersPool = new ObjectPool<MemoryMappedViewAccessor>(
				objectPoolName: $"{nameof(MemoryMappedFileBloomFilter)} readers pool",
				initialCount: initialReaderCount,
				maxCount: maxReaderCount,
				factory: () => _mmf.CreateViewAccessor(Header.Size, 0, MemoryMappedFileAccess.Read),
				dispose: mmfAccessor => mmfAccessor?.Dispose());

			_mmfWriteAccessor = _mmf.CreateViewAccessor(Header.Size, 0, MemoryMappedFileAccess.ReadWrite);
			_readerWriterLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
		}

		/// <summary>
		/// Calculates optimal number of items in the bloom filter for a specific false positive probability, p
		/// </summary>
		/// <param name="p">Desired false positive probability</param>
		/// <returns></returns>
		public long CalculateOptimalNumItems(double p = RecommendedFalsePositiveProbability) {
			return Convert.ToInt64(Math.Floor(
				Math.Log(1 - Math.Pow(p, 1.0 / NumHashFunctions)) /
				Math.Log(Math.Pow(1 - 1.0 / _numBits, NumHashFunctions)
			)));
		}

		public void Add(ReadOnlySpan<byte> bytes) {
			long hash1 = ((long)_hashers[0].Hash(bytes) << 32) | _hashers[1].Hash(bytes);
			long hash2 = ((long)_hashers[2].Hash(bytes) << 32) | _hashers[3].Hash(bytes);

			_readerWriterLock.EnterWriteLock();
			try {
				long hash = hash1;
				for (int i = 0; i < NumHashFunctions; i++) {
					hash += hash2;
					hash &= long.MaxValue; //make non-negative
					long bitPosition = hash % _numBits;
					SetBit(bitPosition, _mmfWriteAccessor);
				}
			} finally {
				_readerWriterLock.ExitWriteLock();
			}
		}

		public bool MayExist(ReadOnlySpan<byte> bytes) {
			long hash1 = ((long)_hashers[0].Hash(bytes) << 32) | _hashers[1].Hash(bytes);
			long hash2 = ((long)_hashers[2].Hash(bytes) << 32) | _hashers[3].Hash(bytes);

			_readerWriterLock.EnterReadLock();
			var readAccessor = _mmfReadersPool.Get();
			try {
				long hash = hash1;
				for (int i = 0; i < NumHashFunctions; i++) {
					hash += hash2;
					hash &= long.MaxValue; //make non-negative
					long bitPosition = hash % _numBits;
					if (!IsBitSet(bitPosition, readAccessor))
						return false;
				}

				return true;
			} finally {
				_mmfReadersPool.Return(readAccessor);
				_readerWriterLock.ExitReadLock();
			}
		}

		public void Flush() {
			_readerWriterLock.EnterWriteLock();
			try {
				_mmfWriteAccessor.Flush();
			} finally {
				_readerWriterLock.ExitWriteLock();
			}
		}

		private static void SetBit(long position, MemoryMappedViewAccessor readWriteAccessor) {
			var bytePosition = position / 8;
			readWriteAccessor.Read(bytePosition, out byte byteValue);
			byteValue = (byte) (byteValue | (1 << (int)(7 - position % 8)));
			readWriteAccessor.Write(bytePosition, byteValue);
		}

		private static bool IsBitSet(long position, MemoryMappedViewAccessor readAccessor) {
			var bytePosition = position / 8;
			readAccessor.Read(bytePosition, out byte byteValue);
			return (byteValue & (1 << (int)(7 - position % 8))) != 0;
		}

		public void Dispose() {
			_mmfWriteAccessor?.Dispose();
			_mmfReadersPool?.Dispose();
			_mmf?.Dispose();
		}
	}
}
