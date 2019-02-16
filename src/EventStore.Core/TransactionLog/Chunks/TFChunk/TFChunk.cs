#pragma warning disable 420

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;
using System.Collections.Concurrent;
using EventStore.Core.TransactionLog.Unbuffered;


namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public unsafe partial class TFChunk : IDisposable {
		public enum ChunkVersions : byte {
			OriginalNotUsed = 1,
			Unaligned = 2,
			Aligned = 3
		}

		public const byte CurrentChunkVersion = 3;
		public const int WriteBufferSize = 8192;
		public const int ReadBufferSize = 8192;

		private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunk>();

		public bool IsReadOnly {
			get { return _isReadOnly; }
		}

		public bool IsCached {
			get { return _isCached != 0; }
		}

		// the logical size of data (could be > PhysicalDataSize if scavenged chunk)
		public long LogicalDataSize {
			get { return Interlocked.Read(ref _logicalDataSize); }
		}

		// the physical size of data size of data
		public int PhysicalDataSize {
			get { return _physicalDataSize; }
		}

		public string FileName {
			get { return _filename; }
		}

		public int FileSize {
			get { return _fileSize; }
		}

		public ChunkHeader ChunkHeader {
			get { return _chunkHeader; }
		}

		public ChunkFooter ChunkFooter {
			get { return _chunkFooter; }
		}

		public readonly int MidpointsDepth;

		public int RawWriterPosition {
			get {
				var writerWorkItem = _writerWorkItem;
				if (writerWorkItem == null)
					throw new InvalidOperationException(string.Format("TFChunk {0} is not in write mode.", _filename));
				return (int)writerWorkItem.StreamPosition;
			}
		}

		private readonly bool _inMem;
		private readonly string _filename;
		private int _fileSize;
		private volatile bool _isReadOnly;
		private ChunkHeader _chunkHeader;
		private ChunkFooter _chunkFooter;

		private readonly int _maxReaderCount;
		private readonly ConcurrentQueue<ReaderWorkItem> _fileStreams = new ConcurrentQueue<ReaderWorkItem>();
		private readonly ConcurrentQueue<ReaderWorkItem> _memStreams = new ConcurrentQueue<ReaderWorkItem>();
		private int _internalStreamsCount;
		private int _fileStreamCount;
		private int _memStreamCount;

		private WriterWorkItem _writerWorkItem;
		private long _logicalDataSize;
		private volatile int _physicalDataSize;

		private volatile IntPtr _cachedData;
		private int _cachedLength;
		private volatile int _isCached;

		private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
		private volatile bool _selfdestructin54321;
		private volatile bool _deleteFile;
		private bool _unbuffered;
		private bool _writeThrough;
		private readonly bool _reduceFileCachePressure;

		private IChunkReadSide _readSide;

		private TFChunk(string filename,
			int initialReaderCount,
			int maxReaderCount,
			int midpointsDepth,
			bool inMem,
			bool unbuffered,
			bool writethrough,
			bool reduceFileCachePressure) {
			Ensure.NotNullOrEmpty(filename, "filename");
			Ensure.Positive(initialReaderCount, "initialReaderCount");
			Ensure.Positive(maxReaderCount, "maxReaderCount");
			if (initialReaderCount > maxReaderCount)
				throw new ArgumentOutOfRangeException("initialReaderCount",
					"initialReaderCount is greater than maxReaderCount.");
			Ensure.Nonnegative(midpointsDepth, "midpointsDepth");

			_filename = filename;
			_internalStreamsCount = initialReaderCount;
			_maxReaderCount = maxReaderCount;
			MidpointsDepth = midpointsDepth;
			_inMem = inMem;
			_unbuffered = unbuffered;
			_writeThrough = writethrough;
			_reduceFileCachePressure = reduceFileCachePressure;
		}

		~TFChunk() {
			FreeCachedData();
		}

		public static TFChunk FromCompletedFile(string filename, bool verifyHash, bool unbufferedRead,
			int initialReaderCount, bool optimizeReadSideCache = false, bool reduceFileCachePressure = false) {
			var chunk = new TFChunk(filename, initialReaderCount, ESConsts.TFChunkMaxReaderCount,
				TFConsts.MidpointsDepth, false, unbufferedRead, false, reduceFileCachePressure);
			try {
				chunk.InitCompleted(verifyHash, optimizeReadSideCache);
			} catch {
				chunk.Dispose();
				throw;
			}

			return chunk;
		}

		public static TFChunk FromOngoingFile(string filename, int writePosition, bool checkSize, bool unbuffered,
			bool writethrough, int initialReaderCount, bool reduceFileCachePressure) {
			var chunk = new TFChunk(filename,
				initialReaderCount,
				ESConsts.TFChunkMaxReaderCount,
				TFConsts.MidpointsDepth,
				false,
				unbuffered,
				writethrough, reduceFileCachePressure);
			try {
				chunk.InitOngoing(writePosition, checkSize);
			} catch {
				chunk.Dispose();
				throw;
			}

			return chunk;
		}

		public static TFChunk CreateNew(string filename,
			int chunkSize,
			int chunkStartNumber,
			int chunkEndNumber,
			bool isScavenged,
			bool inMem,
			bool unbuffered,
			bool writethrough,
			int initialReaderCount,
			bool reduceFileCachePressure) {
			var size = GetAlignedSize(chunkSize + ChunkHeader.Size + ChunkFooter.Size);
			var chunkHeader = new ChunkHeader(CurrentChunkVersion, chunkSize, chunkStartNumber, chunkEndNumber,
				isScavenged, Guid.NewGuid());
			return CreateWithHeader(filename, chunkHeader, size, inMem, unbuffered, writethrough, initialReaderCount,
				reduceFileCachePressure);
		}

		public static TFChunk CreateWithHeader(string filename,
			ChunkHeader header,
			int fileSize,
			bool inMem,
			bool unbuffered,
			bool writethrough,
			int initialReaderCount,
			bool reduceFileCachePressure) {
			var chunk = new TFChunk(filename,
				initialReaderCount,
				ESConsts.TFChunkMaxReaderCount,
				TFConsts.MidpointsDepth,
				inMem,
				unbuffered,
				writethrough,
				reduceFileCachePressure);
			try {
				chunk.InitNew(header, fileSize);
			} catch {
				chunk.Dispose();
				throw;
			}

			return chunk;
		}

		private void InitCompleted(bool verifyHash, bool optimizeReadSideCache) {
			var fileInfo = new FileInfo(_filename);
			if (!fileInfo.Exists)
				throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

			_fileSize = (int)fileInfo.Length;
			_isReadOnly = true;
			SetAttributes(_filename, true);
			CreateReaderStreams();

			var reader = GetReaderWorkItem();
			try {
				_chunkHeader = ReadHeader(reader.Stream);
				Log.Debug("Opened completed {chunk} as version {version}", _filename, _chunkHeader.Version);
				if (_chunkHeader.Version != (byte)ChunkVersions.Unaligned &&
				    _chunkHeader.Version != (byte)ChunkVersions.Aligned)
					throw new CorruptDatabaseException(new WrongFileVersionException(_filename, _chunkHeader.Version,
						CurrentChunkVersion));

				if (_chunkHeader.Version != (byte)ChunkVersions.Aligned && _unbuffered) {
					throw new Exception(
						"You can only run unbuffered mode on v3 or higher chunk files. Please run scavenge on your database to upgrade your transaction file to v3.");
				}

				_chunkFooter = ReadFooter(reader.Stream);
				if (!_chunkFooter.IsCompleted) {
					throw new CorruptDatabaseException(new BadChunkInDatabaseException(
						string.Format("Chunk file '{0}' should be completed, but is not.", _filename)));
				}

				_logicalDataSize = _chunkFooter.LogicalDataSize;
				_physicalDataSize = _chunkFooter.PhysicalDataSize;
				var expectedFileSize = _chunkFooter.PhysicalDataSize + _chunkFooter.MapSize + ChunkHeader.Size +
				                       ChunkFooter.Size;
				if (_chunkHeader.Version == (byte)ChunkVersions.Unaligned && reader.Stream.Length != expectedFileSize) {
					throw new CorruptDatabaseException(new BadChunkInDatabaseException(
						string.Format(
							"Chunk file '{0}' should have a file size of {1} bytes, but it has a size of {2} bytes.",
							_filename,
							expectedFileSize,
							reader.Stream.Length)));
				}
			} finally {
				ReturnReaderWorkItem(reader);
			}

			_readSide = _chunkHeader.IsScavenged
				? (IChunkReadSide)new TFChunkReadSideScavenged(this, optimizeReadSideCache)
				: new TFChunkReadSideUnscavenged(this);
			_readSide.Cache();

			if (verifyHash)
				VerifyFileHash();
		}

		private void InitNew(ChunkHeader chunkHeader, int fileSize) {
			Ensure.NotNull(chunkHeader, "chunkHeader");
			Ensure.Positive(fileSize, "fileSize");

			_fileSize = fileSize;
			_isReadOnly = false;
			_chunkHeader = chunkHeader;
			_physicalDataSize = 0;
			_logicalDataSize = 0;

			if (_inMem)
				CreateInMemChunk(chunkHeader, fileSize);
			else {
				CreateWriterWorkItemForNewChunk(chunkHeader, fileSize);
				SetAttributes(_filename, false);
				CreateReaderStreams();
			}

			_readSide = chunkHeader.IsScavenged
				? (IChunkReadSide)new TFChunkReadSideScavenged(this, false)
				: new TFChunkReadSideUnscavenged(this);
		}

		private void InitOngoing(int writePosition, bool checkSize) {
			Ensure.Nonnegative(writePosition, "writePosition");
			var fileInfo = new FileInfo(_filename);
			if (!fileInfo.Exists)
				throw new CorruptDatabaseException(new ChunkNotFoundException(_filename));

			_fileSize = (int)fileInfo.Length;
			_isReadOnly = false;
			_physicalDataSize = writePosition;
			_logicalDataSize = writePosition;

			SetAttributes(_filename, false);
			CreateWriterWorkItemForExistingChunk(writePosition, out _chunkHeader);
			Log.Trace("Opened ongoing {chunk} as version {version}", _filename, _chunkHeader.Version);
			if (_chunkHeader.Version != (byte)ChunkVersions.Aligned &&
			    _chunkHeader.Version != (byte)ChunkVersions.Unaligned)
				throw new CorruptDatabaseException(new WrongFileVersionException(_filename, _chunkHeader.Version,
					CurrentChunkVersion));
			CreateReaderStreams();

			if (checkSize) {
				var expectedFileSize = _chunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size;
				if (_writerWorkItem.StreamLength != expectedFileSize) {
					throw new CorruptDatabaseException(new BadChunkInDatabaseException(
						string.Format(
							"Chunk file '{0}' should have file size {1} bytes, but instead has {2} bytes length.",
							_filename,
							expectedFileSize,
							_writerWorkItem.StreamLength)));
				}
			}

			_readSide = new TFChunkReadSideUnscavenged(this);
		}


		private void Alignv2File(string filename) {
			//takes a v2 file and aligns it so it can be used with unbuffered
			try {
				SetAttributes(filename, false);
				using (var stream =
					new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) {
					if (stream.Length % 4096 == 0) return;
					var footerStart = stream.Length - ChunkFooter.Size;
					var alignedSize = (stream.Length / 4096 + 1) * 4096;
					var footer = new byte[ChunkFooter.Size];
					stream.SetLength(alignedSize);
					stream.Seek(footerStart, SeekOrigin.Begin);
					stream.Read(footer, 0, ChunkFooter.Size);
					stream.Seek(footerStart, SeekOrigin.Begin);
					var bytes = new byte[alignedSize - footerStart - ChunkFooter.Size];
					stream.Write(bytes, 0, bytes.Length);
					stream.Write(footer, 0, footer.Length);
				}
			} finally {
				SetAttributes(filename, true);
			}
		}

		private void CreateReaderStreams() {
			Interlocked.Add(ref _fileStreamCount, _internalStreamsCount);
			for (int i = 0; i < _internalStreamsCount; i++) {
				_fileStreams.Enqueue(CreateInternalReaderWorkItem());
			}
		}

		private ReaderWorkItem CreateInternalReaderWorkItem() {
			Stream stream;
			if (_unbuffered) {
				stream = UnbufferedFileStream.Create(
					_filename,
					FileMode.Open,
					FileAccess.Read,
					FileShare.ReadWrite,
					false,
					1024 * 1024,
					4096,
					false,
					4096);
			} else {
				stream = new FileStream(
					_filename,
					FileMode.Open,
					FileAccess.Read,
					FileShare.ReadWrite,
					ReadBufferSize,
					_reduceFileCachePressure ? FileOptions.None : FileOptions.RandomAccess);
			}

			var reader = new BinaryReader(stream);
			return new ReaderWorkItem(stream, reader, false);
		}

		private void CreateInMemChunk(ChunkHeader chunkHeader, int fileSize) {
			var md5 = MD5.Create();

			// ALLOCATE MEM
			Interlocked.Exchange(ref _isCached, 1);
			_cachedLength = fileSize;
			_cachedData = Marshal.AllocHGlobal(_cachedLength);

			// WRITER STREAM
			var memStream =
				new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength, _cachedLength, FileAccess.ReadWrite);
			WriteHeader(md5, memStream, chunkHeader);
			memStream.Position = ChunkHeader.Size;

			// READER STREAMS
			Interlocked.Add(ref _memStreamCount, _maxReaderCount);
			for (int i = 0; i < _maxReaderCount; i++) {
				var stream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength);
				var reader = new BinaryReader(stream);
				_memStreams.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
			}

			_writerWorkItem = new WriterWorkItem(null, memStream, md5);
		}

		private void CreateWriterWorkItemForNewChunk(ChunkHeader chunkHeader, int fileSize) {
			var md5 = MD5.Create();

			// create temp file first and set desired length
			// if there is not enough disk space or something else prevents file to be resized as desired
			// we'll end up with empty temp file, which won't trigger false error on next DB verification
			var tempFilename = string.Format("{0}.{1}.tmp", _filename, Guid.NewGuid());
			var tempFile = new FileStream(tempFilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
				WriteBufferSize, FileOptions.SequentialScan);
			tempFile.SetLength(fileSize);

			// we need to write header into temp file before moving it into correct chunk place, so in case of crash
			// we don't end up with seemingly valid chunk file with no header at all...
			WriteHeader(md5, tempFile, chunkHeader);

			tempFile.FlushToDisk();
			tempFile.Close();
			File.Move(tempFilename, _filename);
			Stream stream = GetWriteStream(_filename);
			stream.Position = ChunkHeader.Size;
			_writerWorkItem = new WriterWorkItem(stream, null, md5);
			Flush(); // persist file move result
		}

		private Stream GetWriteStream(string filename) {
			if (!_unbuffered) {
				return new FileStream(
					_filename,
					FileMode.Open,
					FileAccess.ReadWrite,
					FileShare.Read,
					WriteBufferSize,
					FileOptions.SequentialScan);
			} else {
				Log.Trace("Using unbuffered access for TFChunk '{chunk}'...", _filename);
				return UnbufferedFileStream.Create(
					_filename,
					FileMode.Open,
					FileAccess.ReadWrite,
					FileShare.Read,
					false,
					4096 * 1024,
					4096,
					_writeThrough,
					4096);
			}
		}

		private void CreateWriterWorkItemForExistingChunk(int writePosition, out ChunkHeader chunkHeader) {
			var md5 = MD5.Create();
			var stream = GetWriteStream(_filename);
			try {
				chunkHeader = ReadHeader(stream);
				if (chunkHeader.Version == (byte)ChunkVersions.Unaligned) {
					Log.Trace("Upgrading ongoing file {chunk} to version 3", _filename);
					var newHeader = new ChunkHeader((byte)ChunkVersions.Aligned,
						chunkHeader.ChunkSize,
						chunkHeader.ChunkStartNumber,
						chunkHeader.ChunkEndNumber,
						false,
						chunkHeader.ChunkId);
					stream.Seek(0, SeekOrigin.Begin);
					chunkHeader = newHeader;
					var head = newHeader.AsByteArray();
					stream.Write(head, 0, head.Length);
					stream.Flush();
					stream.Seek(0, SeekOrigin.Begin);
				}
			} catch {
				stream.Dispose();
				((IDisposable)md5).Dispose();
				throw;
			}

			var realPosition = GetRawPosition(writePosition);
			MD5Hash.ContinuousHashFor(md5, stream, 0, realPosition);
			stream.Position = realPosition; // this reordering fixes bug in Mono implementation of FileStream
			_writerWorkItem = new WriterWorkItem(stream, null, md5);
		}

		private void WriteHeader(MD5 md5, Stream stream, ChunkHeader chunkHeader) {
			var chunkHeaderBytes = chunkHeader.AsByteArray();
			md5.TransformBlock(chunkHeaderBytes, 0, ChunkHeader.Size, null, 0);
			stream.Write(chunkHeaderBytes, 0, ChunkHeader.Size);
		}

		private void SetAttributes(string filename, bool isReadOnly) {
			if (_inMem)
				return;
			// in mono SetAttributes on non-existing file throws exception, in windows it just works silently.
			Helper.EatException(() => {
				if (isReadOnly)
					File.SetAttributes(filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
				else
					File.SetAttributes(filename, FileAttributes.NotContentIndexed);
			});
		}

		public void VerifyFileHash() {
			if (!IsReadOnly)
				throw new InvalidOperationException("You can't verify hash of not-completed TFChunk.");

			Log.Trace("Verifying hash for TFChunk '{chunk}'...", _filename);
			using (var reader = AcquireReader()) {
				reader.Stream.Seek(0, SeekOrigin.Begin);
				var stream = reader.Stream;
				var footer = _chunkFooter;

				byte[] hash;
				using (var md5 = MD5.Create()) {
					// hash header and data
					MD5Hash.ContinuousHashFor(md5, stream, 0, ChunkHeader.Size + footer.PhysicalDataSize);
					// hash mapping and footer except MD5 hash sum which should always be last
					MD5Hash.ContinuousHashFor(md5,
						stream,
						ChunkHeader.Size + footer.PhysicalDataSize,
						stream.Length - ChunkHeader.Size - footer.PhysicalDataSize - ChunkFooter.ChecksumSize);
					md5.TransformFinalBlock(Empty.ByteArray, 0, 0);
					hash = md5.Hash;
				}

				if (footer.MD5Hash == null || footer.MD5Hash.Length != hash.Length)
					throw new HashValidationException();

				for (int i = 0; i < hash.Length; ++i) {
					if (footer.MD5Hash[i] != hash[i])
						throw new HashValidationException();
				}
			}
		}

		private ChunkHeader ReadHeader(Stream stream) {
			if (stream.Length < ChunkHeader.Size) {
				throw new CorruptDatabaseException(new BadChunkInDatabaseException(
					string.Format("Chunk file '{0}' is too short to even read ChunkHeader, its size is {1} bytes.",
						_filename,
						stream.Length)));
			}

			stream.Seek(0, SeekOrigin.Begin);
			var chunkHeader = ChunkHeader.FromStream(stream);
			return chunkHeader;
		}

		private ChunkFooter ReadFooter(Stream stream) {
			if (stream.Length < ChunkFooter.Size) {
				throw new CorruptDatabaseException(new BadChunkInDatabaseException(
					string.Format("Chunk file '{0}' is too short to even read ChunkFooter, its size is {1} bytes.",
						_filename,
						stream.Length)));
			}

			try {
				stream.Seek(-ChunkFooter.Size, SeekOrigin.End);
				var footer = ChunkFooter.FromStream(stream);
				return footer;
			} catch (Exception ex) {
				throw new Exception("error in chunk file " + _filename, ex);
			}
		}

		private static long GetRawPosition(long logicalPosition) {
			return ChunkHeader.Size + logicalPosition;
		}

		private static long GetDataPosition(WriterWorkItem workItem) {
			return workItem.StreamPosition - ChunkHeader.Size;
		}

		// WARNING CacheInMemory/UncacheFromMemory should not be called simultaneously !!!
		public void CacheInMemory() {
			if (_inMem || Interlocked.CompareExchange(ref _isCached, 1, 0) != 0)
				return;

			// we won the right to cache
			var sw = Stopwatch.StartNew();
			try {
				BuildCacheArray();
			} catch (OutOfMemoryException) {
				Log.Error("CACHING FAILED due to OutOfMemory exception in TFChunk {chunk}.", this);
				_isCached = 0;
				return;
			} catch (FileBeingDeletedException) {
				Log.Debug(
					"CACHING FAILED due to FileBeingDeleted exception (TFChunk is being disposed) in TFChunk {chunk}.",
					this);
				_isCached = 0;
				return;
			}

			Interlocked.Add(ref _memStreamCount, _maxReaderCount);
			if (_selfdestructin54321) {
				if (Interlocked.Add(ref _memStreamCount, -_maxReaderCount) == 0)
					FreeCachedData();
				Log.Trace("CACHING ABORTED for TFChunk {chunk} as TFChunk was probably marked for deletion.", this);
				return;
			}

			var writerWorkItem = _writerWorkItem;
			if (writerWorkItem != null) {
				var memStream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength, _cachedLength,
					FileAccess.ReadWrite);
				memStream.Position = writerWorkItem.StreamPosition;
				writerWorkItem.SetMemStream(memStream);
			}

			for (int i = 0; i < _maxReaderCount; i++) {
				var stream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength);
				var reader = new BinaryReader(stream);
				_memStreams.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
			}

			_readSide.Uncache();

			Log.Trace("CACHED TFChunk {chunk} in {elapsed}.", this, sw.Elapsed);

			if (_selfdestructin54321)
				TryDestructMemStreams();
		}

		private void BuildCacheArray() {
			var workItem = GetReaderWorkItem();
			try {
				if (workItem.IsMemory)
					throw new InvalidOperationException(
						"When trying to build cache, reader worker is already in-memory reader.");

				var dataSize = _isReadOnly ? _physicalDataSize + ChunkFooter.MapSize : _chunkHeader.ChunkSize;
				_cachedLength = GetAlignedSize(ChunkHeader.Size + dataSize + ChunkFooter.Size);
				var cachedData = Marshal.AllocHGlobal(_cachedLength);
				try {
					using (var unmanagedStream = new UnmanagedMemoryStream((byte*)cachedData, _cachedLength,
						_cachedLength, FileAccess.ReadWrite)) {
						workItem.Stream.Seek(0, SeekOrigin.Begin);
						var buffer = new byte[65536];
						// in ongoing chunk there is no need to read everything, it's enough to read just actual data written
						int toRead = _isReadOnly ? _cachedLength : ChunkHeader.Size + _physicalDataSize;
						while (toRead > 0) {
							int read = workItem.Stream.Read(buffer, 0, Math.Min(toRead, buffer.Length));
							if (read == 0)
								break;
							toRead -= read;
							unmanagedStream.Write(buffer, 0, read);
						}
					}
				} catch {
					Marshal.FreeHGlobal(cachedData);
					throw;
				}

				_cachedData = cachedData;
			} finally {
				ReturnReaderWorkItem(workItem);
			}
		}

		//WARNING CacheInMemory/UncacheFromMemory should not be called simultaneously !!!
		public void UnCacheFromMemory() {
			if (_inMem)
				return;
			if (Interlocked.CompareExchange(ref _isCached, 0, 1) == 1) {
				// we won the right to un-cache and chunk was cached
				// NOTE: calling simultaneously cache and uncache is very dangerous
				// NOTE: though multiple simultaneous calls to either Cache or Uncache is ok

				_readSide.Cache();

				var writerWorkItem = _writerWorkItem;
				if (writerWorkItem != null)
					writerWorkItem.DisposeMemStream();

				TryDestructMemStreams();

				Log.Trace("UNCACHED TFChunk {chunk}.", this);
			}
		}

		public bool ExistsAt(long logicalPosition) {
			return _readSide.ExistsAt(logicalPosition);
		}

		public void OptimizeExistsAt() {
			if (!ChunkHeader.IsScavenged) return;
			((TFChunkReadSideScavenged)_readSide).OptimizeExistsAt();
		}

		public void DeOptimizeExistsAt() {
			if (!ChunkHeader.IsScavenged) return;
			((TFChunkReadSideScavenged)_readSide).DeOptimizeExistsAt();
		}

		public RecordReadResult TryReadAt(long logicalPosition) {
			return _readSide.TryReadAt(logicalPosition);
		}

		public RecordReadResult TryReadFirst() {
			return _readSide.TryReadFirst();
		}

		public RecordReadResult TryReadClosestForward(long logicalPosition) {
			return _readSide.TryReadClosestForward(logicalPosition);
		}

		public RecordReadResult TryReadLast() {
			return _readSide.TryReadLast();
		}

		public RecordReadResult TryReadClosestBackward(long logicalPosition) {
			return _readSide.TryReadClosestBackward(logicalPosition);
		}

		public RecordWriteResult TryAppend(LogRecord record) {
			if (_isReadOnly)
				throw new InvalidOperationException("Cannot write to a read-only block.");

			var workItem = _writerWorkItem;
			var buffer = workItem.Buffer;
			var bufferWriter = workItem.BufferWriter;

			buffer.SetLength(4);
			buffer.Position = 4;
			record.WriteTo(bufferWriter);
			var length = (int)buffer.Length - 4;
			bufferWriter.Write(length); // length suffix
			buffer.Position = 0;
			bufferWriter.Write(length); // length prefix

			if (workItem.StreamPosition + length + 2 * sizeof(int) > ChunkHeader.Size + _chunkHeader.ChunkSize)
				return RecordWriteResult.Failed(GetDataPosition(workItem));

			var oldPosition = WriteRawData(workItem, buffer);
			_physicalDataSize = (int)GetDataPosition(workItem); // should fit 32 bits
			_logicalDataSize = ChunkHeader.GetLocalLogPosition(record.LogPosition + length + 2 * sizeof(int));

			// for non-scavenged chunk _physicalDataSize should be the same as _logicalDataSize
			// for scavenged chunk _logicalDataSize should be at least the same as _physicalDataSize
			if ((!ChunkHeader.IsScavenged && _logicalDataSize != _physicalDataSize)
			    || (ChunkHeader.IsScavenged && _logicalDataSize < _physicalDataSize)) {
				throw new Exception(string.Format(
					"Data sizes violation. Chunk: {0}, IsScavenged: {1}, LogicalDataSize: {2}, PhysicalDataSize: {3}.",
					FileName, ChunkHeader.IsScavenged, _logicalDataSize, _physicalDataSize));
			}

			return RecordWriteResult.Successful(oldPosition, _physicalDataSize);
		}

		public bool TryAppendRawData(byte[] buffer) {
			var workItem = _writerWorkItem;
			if (workItem.StreamPosition + buffer.Length > workItem.StreamLength)
				return false;
			WriteRawData(workItem, buffer, buffer.Length);
			return true;
		}

		private static long WriteRawData(WriterWorkItem workItem, MemoryStream buffer) {
			var len = (int)buffer.Length;
			var buf = buffer.GetBuffer();
			return WriteRawData(workItem, buf, len);
		}

		private static long WriteRawData(WriterWorkItem workItem, byte[] buf, int len) {
			var curPos = GetDataPosition(workItem);
			workItem.MD5.TransformBlock(buf, 0, len, null, 0);
			workItem.AppendData(buf, 0, len);
			return curPos;
		}

		public void Flush() {
			if (_inMem)
				return;
			if (_isReadOnly)
				throw new InvalidOperationException("Cannot write to a read-only TFChunk.");
			_writerWorkItem.FlushToDisk();
		}

		public void Complete() {
			if (ChunkHeader.IsScavenged)
				throw new InvalidOperationException("CompleteScavenged should be used for scavenged chunks.");
			CompleteNonRaw(null);
		}

		public void CompleteScavenge(ICollection<PosMap> mapping) {
			if (!ChunkHeader.IsScavenged)
				throw new InvalidOperationException("CompleteScavenged should not be used for non-scavenged chunks.");

			CompleteNonRaw(mapping);
		}

		private void CompleteNonRaw(ICollection<PosMap> mapping) {
			if (_isReadOnly)
				throw new InvalidOperationException("Cannot complete a read-only TFChunk.");

			_chunkFooter = WriteFooter(mapping);
			Flush();
			_isReadOnly = true;

			CleanUpWriterWorkItem(_writerWorkItem);
			_writerWorkItem = null;
			SetAttributes(_filename, true);
		}

		public void CompleteRaw() {
			if (_isReadOnly)
				throw new InvalidOperationException("Cannot complete a read-only TFChunk.");
			if (_writerWorkItem.StreamPosition != _writerWorkItem.StreamLength)
				throw new InvalidOperationException("The raw chunk is not completely written.");
			Flush();
			_chunkFooter = ReadFooter(_writerWorkItem.WorkingStream);
			_isReadOnly = true;

			CleanUpWriterWorkItem(_writerWorkItem);
			_writerWorkItem = null;
			SetAttributes(_filename, true);
		}

		private ChunkFooter WriteFooter(ICollection<PosMap> mapping) {
			var workItem = _writerWorkItem;
			workItem.ResizeStream((int)workItem.StreamPosition);

			int mapSize = 0;
			if (mapping != null) {
				if (!_inMem && _isCached != 0) {
					throw new InvalidOperationException("Trying to write mapping while chunk is cached. "
					                                    + "You probably are writing scavenged chunk as cached. "
					                                    + "Do not do this.");
				}

				mapSize = mapping.Count * PosMap.FullSize;
				workItem.Buffer.SetLength(mapSize);
				workItem.Buffer.Position = 0;
				foreach (var map in mapping) {
					map.Write(workItem.BufferWriter);
				}

				if (_inMem)
					ResizeMemStream(workItem, mapSize);
				WriteRawData(workItem, workItem.Buffer);
			}

			workItem.FlushToDisk();

			if (_chunkHeader.Version >= (byte)ChunkVersions.Aligned) {
				var alignedSize = GetAlignedSize(ChunkHeader.Size + _physicalDataSize + mapSize + ChunkFooter.Size);
				var bufferSize = alignedSize - workItem.StreamPosition - ChunkFooter.Size;
				Log.Debug("Buffer size is {bufferSize}", bufferSize);
				if (bufferSize > 0) {
					byte[] buffer = new byte[bufferSize];
					WriteRawData(workItem, buffer, buffer.Length);
				}
			}

			Flush();

			var footerNoHash = new ChunkFooter(true, true, _physicalDataSize, LogicalDataSize, mapSize,
				new byte[ChunkFooter.ChecksumSize]);
			//MD5
			workItem.MD5.TransformFinalBlock(footerNoHash.AsByteArray(), 0,
				ChunkFooter.Size - ChunkFooter.ChecksumSize);
			//FILE
			var footerWithHash =
				new ChunkFooter(true, true, _physicalDataSize, LogicalDataSize, mapSize, workItem.MD5.Hash);
			workItem.AppendData(footerWithHash.AsByteArray(), 0, ChunkFooter.Size);

			Flush();

			_fileSize = (int)workItem.StreamLength;
			return footerWithHash;
		}

		private void ResizeMemStream(WriterWorkItem workItem, int mapSize) {
			var newFileSize = (int)workItem.StreamPosition + mapSize + ChunkFooter.Size;
			if (workItem.StreamLength < newFileSize) {
				var pos = workItem.StreamPosition;
				var newCachedData = Marshal.AllocHGlobal(newFileSize);
				var memStream = new UnmanagedMemoryStream((byte*)newCachedData,
					workItem.StreamLength,
					newFileSize,
					FileAccess.ReadWrite);
				workItem.WorkingStream.Position = 0;
				workItem.WorkingStream.CopyTo(memStream);

				if (!TryDestructMemStreams())
					throw new Exception("MemStream readers are in use when writing scavenged chunk.");

				_cachedLength = newFileSize;
				_cachedData = newCachedData;

				memStream.Position = pos;
				workItem.SetMemStream(memStream);

				// READER STREAMS
				Interlocked.Add(ref _memStreamCount, _maxReaderCount);
				for (int i = 0; i < _maxReaderCount; i++) {
					var stream = new UnmanagedMemoryStream((byte*)_cachedData, _cachedLength);
					var reader = new BinaryReader(stream);
					_memStreams.Enqueue(new ReaderWorkItem(stream, reader, isMemory: true));
				}
			}
		}

		private void CleanUpWriterWorkItem(WriterWorkItem writerWorkItem) {
			if (writerWorkItem == null)
				return;
			writerWorkItem.Dispose();
		}

		public void Dispose() {
			_selfdestructin54321 = true;
			TryDestructFileStreams();
			TryDestructMemStreams();
		}

		public void MarkForDeletion() {
			_selfdestructin54321 = true;
			_deleteFile = true;
			TryDestructFileStreams();
			TryDestructMemStreams();
		}

		private void TryDestructFileStreams() {
			int fileStreamCount = int.MaxValue;

			ReaderWorkItem workItem;
			while (_fileStreams.TryDequeue(out workItem)) {
				workItem.Stream.Dispose();
				fileStreamCount = Interlocked.Decrement(ref _fileStreamCount);
			}

			if (fileStreamCount < 0)
				throw new Exception("Count of file streams reduced below zero.");
			if (fileStreamCount == 0) // we are the last who should "turn the light off" for file streams
				CleanUpFileStreamDestruction();
		}

		private void CleanUpFileStreamDestruction() {
			CleanUpWriterWorkItem(_writerWorkItem);

			if (!_inMem) {
				Helper.EatException(() => File.SetAttributes(_filename, FileAttributes.Normal));

				if (_deleteFile) {
					Log.Info("File {chunk} has been marked for delete and will be deleted in TryDestructFileStreams.",
						Path.GetFileName(_filename));
					Helper.EatException(() => File.Delete(_filename));
				}
			}

			_destroyEvent.Set();
		}

		public static int GetAlignedSize(int size) {
			if (size % 4096 == 0) return size;
			return (size / 4096 + 1) * 4096;
		}

		private bool TryDestructMemStreams() {
			var writerWorkItem = _writerWorkItem;
			if (writerWorkItem != null)
				writerWorkItem.DisposeMemStream();

			int memStreamCount = int.MaxValue;

			ReaderWorkItem workItem;
			while (_memStreams.TryDequeue(out workItem)) {
				memStreamCount = Interlocked.Decrement(ref _memStreamCount);
			}

			if (memStreamCount < 0)
				throw new Exception("Count of memory streams reduced below zero.");
			if (memStreamCount == 0) // we are the last who should "turn the light off" for memory streams
			{
				FreeCachedData();
				return true;
			}

			return false;
		}

		private void FreeCachedData() {
			var cachedData = Interlocked.Exchange(ref _cachedData, IntPtr.Zero);
			if (cachedData != IntPtr.Zero)
				Marshal.FreeHGlobal(cachedData);
		}

		public void WaitForDestroy(int timeoutMs) {
			if (!_destroyEvent.Wait(timeoutMs))
				throw new TimeoutException();
		}

		private ReaderWorkItem GetReaderWorkItem() {
			if (_selfdestructin54321)
				throw new FileBeingDeletedException();

			ReaderWorkItem item;
			// try get memory stream reader first
			if (_memStreams.TryDequeue(out item))
				return item;

			if (_inMem)
				throw new Exception("Not enough memory streams during in-mem TFChunk mode.");

			if (_fileStreams.TryDequeue(out item))
				return item;

			if (_selfdestructin54321)
				throw new FileBeingDeletedException();

			var internalStreamCount = Interlocked.Increment(ref _internalStreamsCount);
			if (internalStreamCount > _maxReaderCount)
				throw new Exception("Unable to acquire reader work item. Max internal streams limit reached.");

			Interlocked.Increment(ref _fileStreamCount);
			if (_selfdestructin54321) {
				if (Interlocked.Decrement(ref _fileStreamCount) == 0)
					CleanUpFileStreamDestruction(); // now we should "turn light off"
				throw new FileBeingDeletedException();
			}

			// if we get here, then we reserved TFChunk for sure so no one should dispose of chunk file
			// until client returns the reader
			return CreateInternalReaderWorkItem();
		}

		private void ReturnReaderWorkItem(ReaderWorkItem item) {
			if (item.IsMemory) {
				_memStreams.Enqueue(item);
				if (_isCached == 0 || _selfdestructin54321)
					TryDestructMemStreams();
			} else {
				_fileStreams.Enqueue(item);
				if (_selfdestructin54321)
					TryDestructFileStreams();
			}
		}

		public TFChunkBulkReader AcquireReader() {
			Interlocked.Increment(ref _fileStreamCount);
			if (_selfdestructin54321) {
				if (Interlocked.Decrement(ref _fileStreamCount) == 0) {
					// now we should "turn light off"
					CleanUpFileStreamDestruction();
				}

				throw new FileBeingDeletedException();
			}

			// if we get here, then we reserved TFChunk for sure so no one should dispose of chunk file
			// until client returns dedicated reader
			return new TFChunkBulkReader(this, GetSequentialReaderFileStream());
		}

		private Stream GetSequentialReaderFileStream() {
			return _inMem
				? (Stream)new UnmanagedMemoryStream((byte*)_cachedData, _fileSize)
				: new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536,
					FileOptions.SequentialScan);
		}

		public void ReleaseReader(TFChunkBulkReader reader) {
			var fileStreamCount = Interlocked.Decrement(ref _fileStreamCount);
			if (fileStreamCount < 0)
				throw new Exception("Count of file streams reduced below zero.");
			if (_selfdestructin54321 && fileStreamCount == 0)
				CleanUpFileStreamDestruction();
		}

		public override string ToString() {
			return string.Format("#{0}-{1} ({2})", _chunkHeader.ChunkStartNumber, _chunkHeader.ChunkEndNumber,
				Path.GetFileName(_filename));
		}

		private struct Midpoint {
			public readonly int ItemIndex;
			public readonly long LogPos;

			public Midpoint(int itemIndex, PosMap posmap) {
				ItemIndex = itemIndex;
				LogPos = posmap.LogPos;
			}

			public override string ToString() {
				return string.Format("ItemIndex: {0}, LogPos: {1}", ItemIndex, LogPos);
			}
		}
	}
}
