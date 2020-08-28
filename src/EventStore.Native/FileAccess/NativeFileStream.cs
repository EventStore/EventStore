using System;
using System.IO;


namespace EventStore.Native.FileAccess {
	public unsafe class NativeFileStream : Stream {
		private readonly NativeFile _nativeFile;

		public NativeFileStream(string path) :
			this(path, System.IO.FileAccess.ReadWrite) { }

		public NativeFileStream(
					string path,
					System.IO.FileAccess access,
					FileMode mode = FileMode.OpenOrCreate,
					FileShare share = FileShare.ReadWrite) {
			_nativeFile = NativeFile.OpenFile(path, access, mode, share);
			_canWrite = access == System.IO.FileAccess.Write || access == System.IO.FileAccess.ReadWrite;
			var minLength = _nativeFile.AlignToSectorSize(1);
			if ((access == System.IO.FileAccess.ReadWrite ||
				 access == System.IO.FileAccess.Write) &&
				_nativeFile.Length < minLength) {
				_nativeFile.SetLength(minLength);
			}
		}

		public override long Seek(long offset, SeekOrigin origin) {
			CheckDisposed();
			_position = _nativeFile.Seek(offset, origin);
			return _position;
		}

		public override void Flush() {
			CheckDisposed();
			_nativeFile.Flush();
		}

		public override void SetLength(long value) {
			CheckDisposed();
			_nativeFile.SetLength(value);
			if (_position > value) {
				_position = _nativeFile.AlignToSectorSize(value);
			}
		}

		public override int Read(byte[] buffer, int offset, int count) {
			CheckDisposed();
			//todo-clc: remove this: fixing things via the GC is extremely slow, fix with buffer management
			fixed (byte* b = buffer) {
				var p = b;
				p += offset;
				var read = _nativeFile.Read(_position, (IntPtr)p, count);
				_position += read;
				return read;
			}
		}
		public void Write(byte* buffer, int offset, int count) {
			CheckDisposed();
			if (!_canWrite) {
				throw new UnauthorizedAccessException($"{nameof(NativeFileStream)}:Write Cannot write, file not opened for writing!");
			}

			buffer += offset;
			if (Length < _position + count) {
				SetLength(_position + count);
			}

			var written = _nativeFile.Write(_position, (IntPtr)buffer, count);
			_position += written;
		}

		
		public override void Write(byte[] buffer, int offset, int count) {
			//todo-clc: fixed is slow extremely slow, fix with buffer management!!!
			fixed (byte* b = buffer) {
				Write(b,offset,count);
			}
		}
		public override bool CanRead {
			get {
				CheckDisposed();
				return true;
			}
		}

		public override bool CanSeek {
			get {
				CheckDisposed();
				return true;
			}
		}

		private bool _canWrite = false;
		public override bool CanWrite => _canWrite;

		public override long Length {
			get {
				CheckDisposed();
				return _nativeFile.Length;
			}
		}

		private long _position;
		public override long Position {
			get {
				CheckDisposed();
				return _position;
			}
			set {
				CheckDisposed();
				if (_position == value)
					return;
				_position = Seek(value, SeekOrigin.Begin);
			}
		}




		[System.Diagnostics.Conditional("DEBUG")]
		private void CheckDisposed() {
			//only check in debug
			if (!_nativeFile.IsOpen())
				throw new ObjectDisposedException("object is disposed.");
		}

		private bool _disposed;
		protected override void Dispose(bool disposing) {
			if (_disposed)
				return;
			Flush();
			_nativeFile.Dispose();
			GC.SuppressFinalize(this);
			_disposed = true;
		}
	}
}
