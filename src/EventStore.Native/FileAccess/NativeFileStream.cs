using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace EventStore.Native.FileAccess {
	public unsafe class NativeFileStream : Stream {
		private readonly NativeFile _nativeFile;

		public NativeFileStream(string path) {
			_nativeFile = NativeFile.OpenFile(path);
		}
		public override long Seek(long offset, SeekOrigin origin) {
			CheckDisposed();
			Position = _nativeFile.Seek(offset, origin);
			return Position;
		}

		public override void Flush() {
			CheckDisposed();
			_nativeFile.Flush();
		}

		public override void SetLength(long value) {
			CheckDisposed();
			_nativeFile.SetLength(value);
			Seek(value, SeekOrigin.Begin);
		}

		public override int Read(byte[] buffer, int offset, int count) {
			CheckDisposed();
			//todo-clc: fixed is slow as fuck!!!
			fixed (byte* p = buffer) {
				var read = _nativeFile.Read(Position, (IntPtr)p, count);
				Position += read;
				return read;
			}
		}

		public override unsafe void Write(byte[] buffer, int offset, int count) {
			CheckDisposed();
			//todo-clc: fixed is slow as fuck!!!
			fixed (byte* p = buffer) {
				var written = _nativeFile.Write(Position, (IntPtr)p, count);
				Position += written;
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

		public override bool CanWrite {
			get {
				CheckDisposed();
				return true;
			}
		}

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
