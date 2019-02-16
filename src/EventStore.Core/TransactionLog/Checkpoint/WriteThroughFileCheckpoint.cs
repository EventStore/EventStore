using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Checkpoint {
	static class Filenative {
		[DllImport("kernel32", SetLastError = true)]
		internal static extern SafeFileHandle CreateFile(
			string FileName,
			uint DesiredAccess,
			uint ShareMode,
			IntPtr SecurityAttributes,
			uint CreationDisposition,
			int FlagsAndAttributes,
			IntPtr hTemplate
		);

		public const int FILE_FLAG_NO_BUFFERING = 0x20000000;
	}

	public class WriteThroughFileCheckpoint : ICheckpoint {
		private readonly string _filename;
		private readonly string _name;
		private readonly bool _cached;
		private long _last;
		private long _lastFlushed;
		private FileStream _stream;
		private readonly BinaryWriter _writer;
		private readonly BinaryReader _reader;
		private readonly MemoryStream _memStream;
		private readonly byte[] buffer;

		public WriteThroughFileCheckpoint(string filename)
			: this(filename, Guid.NewGuid().ToString(), false) {
		}

		public WriteThroughFileCheckpoint(string filename, string name) : this(filename, name, false) {
		}

		public WriteThroughFileCheckpoint(string filename, string name, bool cached, long initValue = 0) {
			_filename = filename;
			_name = name;
			_cached = cached;
			buffer = new byte[4096];
			_memStream = new MemoryStream(buffer);

			var handle = Filenative.CreateFile(_filename,
				(uint)FileAccess.ReadWrite,
				(uint)FileShare.ReadWrite,
				IntPtr.Zero,
				(uint)FileMode.OpenOrCreate,
				Filenative.FILE_FLAG_NO_BUFFERING | (int)FileOptions.WriteThrough,
				IntPtr.Zero);

			_stream = new FileStream(handle, FileAccess.ReadWrite, 4096);
			var exists = _stream.Length == 4096;
			_stream.SetLength(4096);
			_reader = new BinaryReader(_stream);
			_writer = new BinaryWriter(_memStream);
			if (!exists) {
				Write(initValue);
				Flush();
			}

			_last = _lastFlushed = ReadCurrent();
		}


		[DllImport("kernel32.dll")]
		static extern bool FlushFileBuffers(IntPtr hFile);

		public void Close() {
			Flush();
			_stream.Close();
			_stream.Dispose();
		}

		public string Name {
			get { return _name; }
		}

		public void Write(long checkpoint) {
			Interlocked.Exchange(ref _last, checkpoint);
		}

		public void Flush() {
			_memStream.Seek(0, SeekOrigin.Begin);
			_stream.Seek(0, SeekOrigin.Begin);
			var last = Interlocked.Read(ref _last);
			_writer.Write(last);
			_stream.Write(buffer, 0, buffer.Length);

			Interlocked.Exchange(ref _lastFlushed, last);

			OnFlushed(last);
			//FlushFileBuffers(_file.SafeMemoryMappedFileHandle.DangerousGetHandle());
		}

		public long Read() {
			return _cached ? Interlocked.Read(ref _lastFlushed) : ReadCurrent();
		}

		private long ReadCurrent() {
			_stream.Seek(0, SeekOrigin.Begin);
			return _reader.ReadInt64();
		}

		public long ReadNonFlushed() {
			return Interlocked.Read(ref _last);
		}

		public event Action<long> Flushed;

		protected virtual void OnFlushed(long obj) {
			var onFlushed = Flushed;
			if (onFlushed != null)
				onFlushed.Invoke(obj);
		}

		public void Dispose() {
			Close();
		}
	}
}
