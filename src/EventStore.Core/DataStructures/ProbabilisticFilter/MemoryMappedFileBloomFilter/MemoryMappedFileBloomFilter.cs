using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public unsafe class MemoryMappedFileBloomFilter : IProbabilisticFilter, IDisposable {
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
		private readonly byte* _pointer = null;

		private readonly IHasher[] _hashers = {
			new XXHashUnsafe(seed: 0xC0015EEDU),
			new Murmur3AUnsafe(seed: 0xC0015EEDU),
			new XXHashUnsafe(seed: 0x13375EEDU),
			new Murmur3AUnsafe(seed: 0x13375EEDU)
		};

		private readonly FileStream _fileStream;
		private readonly MemoryMappedFile _mmf;
		private readonly MemoryMappedViewAccessor _mmfWriteAccessor;
		private readonly object _writeLock = new();

		private bool _disposed;

		/// <summary>
		/// Bloom filter implementation which uses a memory-mapped file for persistence.
		/// </summary>
		/// <param name="path">Path to the bloom filter file</param>
		/// <param name="size">Size of the bloom filter in bytes</param>
		public MemoryMappedFileBloomFilter(string path, bool create, long size) {
			Ensure.NotNull(path, nameof(path));

			if (size < MinSizeKB * 1000 || size > MaxSizeKB * 1000) {
				throw new ArgumentOutOfRangeException(nameof(size), $"size should be between {MinSizeKB:N0} and {MaxSizeKB:N0} KB inclusive");
			}

			_numBits = size * 8;

			_fileStream = new FileStream(
				path,
				// todo: OpenOrCreate can be CreateNew if the mininode closed the file before declaring itself 'stopped'
				create ? FileMode.OpenOrCreate : FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.ReadWrite);

			var fileSize = Header.Size + _numBits / 8;
			_fileStream.SetLength(fileSize);

			_mmf = MemoryMappedFile.CreateFromFile(
				fileStream: _fileStream,
				mapName: null,
				capacity: 0,
				access: MemoryMappedFileAccess.ReadWrite,
				inheritability: HandleInheritability.None,
				leaveOpen: false);

			_mmfWriteAccessor = _mmf.CreateViewAccessor(Header.Size, 0, MemoryMappedFileAccess.ReadWrite);
			_mmfWriteAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _pointer);

			if (create) {
				// fill with zeros
				var bytesToClear = fileSize;
				var startAddress = _pointer;
				while (bytesToClear > 0) {
					uint bytesToClearInBlock = bytesToClear > uint.MaxValue ? uint.MaxValue : (uint)bytesToClear;
					Unsafe.InitBlock(ref *startAddress, 0, bytesToClearInBlock);
					startAddress += bytesToClearInBlock;
					bytesToClear -= bytesToClearInBlock;
				}

				Header header = new() {
					Version = Header.CurrentVersion,
					NumBits = _numBits
				};

				header.WriteTo(_mmf);
			} else {
				try {
					var header = Header.ReadFrom(_mmf);
					if (header.NumBits != _numBits) {
						throw new SizeMismatchException(
							$"The configured number of bytes ({(_numBits / 8):N0}) does not match the number of bytes in file ({(header.NumBits / 8):N0}).");
					}
				} catch {
					Dispose();
					throw;
				}
			}
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

			// guarantee only one writer
			lock (_writeLock) {
				long hash = hash1;
				// possible SIMD optimisation?
				for (int i = 0; i < NumHashFunctions; i++) {
					hash += hash2;
					hash &= long.MaxValue; // make non-negative
					long bitPosition = hash % _numBits;
					SetBit(bitPosition);
				}
			}
		}

		public bool MightContain(ReadOnlySpan<byte> bytes) {
			long hash1 = ((long)_hashers[0].Hash(bytes) << 32) | _hashers[1].Hash(bytes);
			long hash2 = ((long)_hashers[2].Hash(bytes) << 32) | _hashers[3].Hash(bytes);

			Thread.MemoryBarrier();

			long hash = hash1;
			for (int i = 0; i < NumHashFunctions; i++) {
				hash += hash2;
				hash &= long.MaxValue; // make non-negative
				long bitPosition = hash % _numBits;
				if (!IsBitSet(bitPosition))
					return false;
			}

			return true;
		}

		public void Flush() {
			Thread.MemoryBarrier();
			_mmfWriteAccessor.Flush();
			_fileStream.FlushToDisk();
			Thread.MemoryBarrier();
		}

		private void SetBit(long bitPosition) {
			var bytePosition = bitPosition / 8;
			ref var byteValue = ref *(_pointer + Header.Size + bytePosition);
			byteValue = (byte) (byteValue | (1 << (int)(7 - bitPosition % 8)));
		}

		private bool IsBitSet(long bitPosition) {
			var bytePosition = bitPosition / 8;
			var byteValue = *(_pointer + Header.Size + bytePosition);
			return (byteValue & (1 << (int)(7 - bitPosition % 8))) != 0;
		}

		public void Dispose() {
			if (_disposed)
				return;

			_disposed = true;

			if (_pointer != null) {
				_mmfWriteAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
			}

			_mmfWriteAccessor?.Dispose();
			_mmf?.Dispose();
		}
	}
}
