using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace EventStore.Native.FileAccess {
	public unsafe class NativeFileStream : Stream {
		private readonly NativeFile _nativeFile;

		public NativeFileStream(string path, bool readOnly = true) {
			_nativeFile = NativeFile.OpenFile(path, readOnly);
			_canWrite = !readOnly;
			var minLength = _nativeFile.AlignToSectorSize(1);
			if (_canWrite && _nativeFile.Length < minLength) {

				_nativeFile.SetLength(minLength);
			}
		}

		public override long Seek(long offset, SeekOrigin origin) {
			//todo-clc: why is this not implemented in Unbuffered file?
			//throw to match current implementation
			if (origin == SeekOrigin.Current) { throw new NotImplementedException(); }

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
			//todo-clc: remove this: fixing things via the GC is slow as fuck!!!
			fixed (byte* b = buffer) {
				var p = b;
				p += offset;
				var read = _nativeFile.Read(_position, (IntPtr)p, count);
				_position += read;
				return read;
			}
		}

		public override void Write(byte[] buffer, int offset, int count) {
			CheckDisposed();
			if (!_canWrite) {
				throw new UnauthorizedAccessException($"{nameof(NativeFileStream)}:Write Cannot write, file not opened for writing!");
			}

			//todo-clc: fixed is slow as fuck!!!
			fixed (byte* b = buffer) {
				var p = b;
				p += offset;
				if (Length < _position + count) {
					SetLength(_position + count);
				}

				var written = _nativeFile.Write(_position, (IntPtr)p, count);
				_position += written;
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

		private long _position = 0;
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
