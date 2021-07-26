using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;
using Serilog;

namespace EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter {
	public unsafe class MemoryMappedFileBloomFilter : IProbabilisticFilter, IDisposable {
		/*
		    Bloom filter implementation based on the following paper by Adam Kirsch and Michael Mitzenmacher:
		    "Less Hashing, Same Performance: Building a Better Bloom Filter"
		    https://www.eecs.harvard.edu/~michaelm/postscripts/rsa2008.pdf

		    Only two 32-bit hash functions can be used to simulate additional hash functions of the form g(x) = h1(x) + i*h2(x)

		    First cache line (64 bytes) contains the header and nothing else
		    Subsequent cachelines contain 60 bytes of bloom filter bits then a 4 byte hash of those bits.
		*/
		protected static readonly ILogger Log = Serilog.Log.ForContext<MemoryMappedFileBloomFilter>();
		public const long MinSizeKB = 10;
		public const long MaxSizeKB = 4_000_000;
		public const double RecommendedFalsePositiveProbability = 0.02;
		private const int CacheLineSize = BloomFilterIntegrity.CacheLineSize;
		private const int NumHashFunctions = 6;
		private readonly long _numBits;
		private readonly Header _header;
		private readonly byte* _pointer = null;

		private readonly IHasher[] _hashers = {
			new XXHashUnsafe(seed: 0xC0015EEDU),
			new Murmur3AUnsafe(seed: 0xC0015EEDU),
			new XXHashUnsafe(seed: 0x13375EEDU),
			new Murmur3AUnsafe(seed: 0x13375EEDU)
		};

		private readonly FileStream _fileStream;
		private readonly long _fileSize;
		private readonly MemoryMappedFile _mmf;
		private readonly MemoryMappedViewAccessor _mmfWriteAccessor;
		private readonly object _writeLock = new();

		private bool _disposed;

		public int CorruptionRebuildCount => _header.CorruptionRebuildCount;

		/// <summary>
		/// Bloom filter implementation which uses a memory-mapped file for persistence.
		/// </summary>
		/// <param name="path">Path to the bloom filter file</param>
		/// <param name="size">Size of the bloom filter in bytes</param>
		///
		/// Add and MightContain can be called concurrently on different threads.
		/// If the call to Add is complete then a call to MightContain will find the added value.
		public MemoryMappedFileBloomFilter(string path, bool create, long size, int corruptionRebuildCount = 0) {
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

			if (Header.Size > CacheLineSize)
				throw new Exception("header needs to fit in a cache line");

			// add room for filter bits, rounded up to nearest cacheline
			_fileSize = GetBytePositionInFile(_numBits) / CacheLineSize * CacheLineSize;
			_fileSize += CacheLineSize;
			_fileStream.SetLength(_fileSize);

			_mmf = MemoryMappedFile.CreateFromFile(
				fileStream: _fileStream,
				mapName: null,
				capacity: 0,
				access: MemoryMappedFileAccess.ReadWrite,
				inheritability: HandleInheritability.None,
				leaveOpen: false);

			_mmfWriteAccessor = _mmf.CreateViewAccessor(CacheLineSize, 0, MemoryMappedFileAccess.ReadWrite);
			_mmfWriteAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _pointer);

			if (create) {
				FillWithZeros();

				_header = new Header() {
					Version = Header.CurrentVersion,
					CorruptionRebuildCount = corruptionRebuildCount,
					NumBits = _numBits
				};

				_header.WriteTo(_mmf);
			} else {
				try {
					_header = Header.ReadFrom(_mmf);
					if (_header.NumBits != _numBits) {
						throw new SizeMismatchException(
							$"The configured number of bytes ({(_numBits / 8):N0}) does not match the number of bytes in file ({(_header.NumBits / 8):N0}).");
					}
					Verify(0.05);
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
			void SetBit(long bitPosition) {
				var bytePositionInFile = GetBytePositionInFile(bitPosition);
				ref var byteValue = ref ReadByte(bytePositionInFile);
				byteValue = byteValue.SetBit(bitPosition % 8);
				var cacheLine = ReadCacheLineFor(bytePositionInFile);
				BloomFilterIntegrity.WriteHash(cacheLine);
			}

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
				// lock release guarantees our STOREs are executed before we return
				// todo: confirm is this is true on ARM
			}
		}

		public bool MightContain(ReadOnlySpan<byte> bytes) {
			bool IsBitSet(long bitPosition) {
				var bytePositionInFile = GetBytePositionInFile(bitPosition);
				var byteValue = ReadByte(bytePositionInFile);
				return byteValue.IsBitSet(bitPosition % 8);
			}

			long hash1 = ((long)_hashers[0].Hash(bytes) << 32) | _hashers[1].Hash(bytes);
			long hash2 = ((long)_hashers[2].Hash(bytes) << 32) | _hashers[3].Hash(bytes);

			// guarantee our LOADs are executed now and not prior.
			// dual with the memory barrier implicit in the Add(...) method
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

		private static long GetBytePositionInFile(long bitPosition) {
			var bytePositionInFile = bitPosition / 8;

			// account for 4 bytes at the end of each cache line being a hash
			bytePositionInFile += bytePositionInFile / (CacheLineSize - BloomFilterIntegrity.HashSize) * BloomFilterIntegrity.HashSize;

			// first cache line is reserved for the header
			bytePositionInFile += CacheLineSize;

			return bytePositionInFile;
		}

		private ref byte ReadByte(long bytePositionInFile) {
			EnsureBounds(bytePositionInFile, length: 1);
			return ref *(_pointer + bytePositionInFile);
		}

		private Span<byte> ReadCacheLineFor(long bytePositionInFile) {
			bytePositionInFile = bytePositionInFile / CacheLineSize * CacheLineSize;
			EnsureBounds(bytePositionInFile, CacheLineSize);
			return new Span<byte>(_pointer + bytePositionInFile, CacheLineSize);
		}

		private void EnsureBounds(long bytePosition, int length) {
			if (bytePosition > _fileSize - length)
				throw new ArgumentOutOfRangeException(
					nameof(bytePosition),
					bytePosition,
					$"{bytePosition:N0}/{_fileSize:N0}");
		}

		public void Flush() {
			// need to be sure the read operations were not done in advance of this point
			// not 100% sure, however, that the barrier is required.
			Thread.MemoryBarrier();
			_mmfWriteAccessor.Flush();
			_fileStream.FlushToDisk();
		}

		public void Dispose() {
			if (_disposed)
				return;

			_disposed = true;

			_mmfWriteAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
			_mmfWriteAccessor?.Dispose();
			_mmf?.Dispose();
		}

		private void FillWithZeros() {
			Log.Debug("Zeroing bloom filter...");
			var bytesToClear = _fileSize;
			var startAddress = _pointer;
			while (bytesToClear > 0) {
				uint bytesToClearInBlock = bytesToClear > uint.MaxValue ? uint.MaxValue : (uint)bytesToClear;
				Unsafe.InitBlock(ref *startAddress, 0, bytesToClearInBlock);
				startAddress += bytesToClearInBlock;
				bytesToClear -= bytesToClearInBlock;
			}
			Log.Debug("Done zeroing bloom filter");
		}

		// corruptionThreshold = 0.05 => tolerate up to 5% corruption
		public void Verify(double corruptionThreshold) {
			Ensure.Nonnegative(corruptionThreshold, nameof(corruptionThreshold));

			Log.Debug("Verifying bloom filter...");
			var corruptedCacheLines = 0;

			for (int i = CacheLineSize; i < _fileSize; i += CacheLineSize) {
				var cacheLine = ReadCacheLineFor(i);
				if (!BloomFilterIntegrity.ValidateHash(cacheLine)) {
					corruptedCacheLines++;
					Log.Verbose("Invalid cacheline starting at {i:N0} / {fileSize:N0}", i, _fileSize);
				}
			}

			var totalCacheLines = _fileSize / CacheLineSize;

			var corruptionThresholdPercent = corruptionThreshold * 100;
			var corruptedPercent = (double)corruptedCacheLines / totalCacheLines * 100;
			if (corruptedPercent > corruptionThreshold * 100)
				throw new CorruptedHashException(
					_header.CorruptionRebuildCount,
					$"{corruptedCacheLines:N0} corruptions detected ({corruptedPercent:N2}%). Threshold {corruptionThresholdPercent:N2}%");

			if (corruptedCacheLines == 0) {
				Log.Debug("Done verifying bloom filter");
			} else {
				Log.Warning("Done verifying bloom filter: {corruptedCacheLines:N0} corruptions detected ({corruptedPercent:N2}%). Threshold {corruptionThresholdPercent:N2}%",
					corruptedCacheLines,
					corruptedPercent,
					corruptionThresholdPercent);
			}
		}
	}
}
