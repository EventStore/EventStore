using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Unbuffered
{
    //NOTE THIS DOES NOT SUPPORT ALL STREAM OPERATIONS AS YOU MIGHT EXPECT IT SUPPORTS WHAT WE USE!
    public unsafe class ReadUnbufferedFileStream : Stream
    {
        private byte* _readBuffer;
        private readonly int _readBufferSize;
        private readonly IntPtr _readBufferOriginal;
        private readonly uint _blockSize;
        private int _bufferedCount;
        private SafeFileHandle _handle;
        private long _readLocation = -1;
        private long _lastPosition;

        private ReadUnbufferedFileStream(SafeFileHandle handle, uint blockSize, int internalReadBufferSize)
        {
            _handle = handle;
            _readBufferSize = internalReadBufferSize;
            _readBufferOriginal = Marshal.AllocHGlobal((int)(internalReadBufferSize + blockSize));
            _readBuffer = Align(_readBufferOriginal, blockSize);
            _blockSize = blockSize;
        }

        private byte* Align(IntPtr buf, uint alignTo)
        {
            //This makes an aligned buffer linux needs this.
            //The buffer must originally be at least one alignment bigger!
            var diff = alignTo - (buf.ToInt64() % alignTo);
            var aligned = (IntPtr)(buf.ToInt64() + diff);
            return (byte*)aligned;
        }

        public static ReadUnbufferedFileStream Create(string path,
            FileShare share,
            bool sequential,
            int internalReadBufferSize,
            uint minBlockSize)
        {
            var blockSize = NativeFile.GetDriveSectorSize(path);
            blockSize = blockSize > minBlockSize ? blockSize : minBlockSize;
            if (internalReadBufferSize % blockSize != 0)
                throw new Exception("read buffer size must be aligned to block size of " + blockSize + " bytes");

            var handle = NativeFile.CreateUnbufferedRW(path, FileAccess.Read, share, FileMode.Open, false);
            return new ReadUnbufferedFileStream(handle, blockSize, internalReadBufferSize);
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        private static void MemCopy(byte[] src, long srcOffset, byte* dest, long destOffset, long count)
        {
            fixed (byte* p = src)
            {
                MemCopy(p, srcOffset, dest, destOffset, count);
            }
        }

        private static void MemCopy(byte* src, long srcOffset, byte[] dest, long destOffset, long count)
        {
            fixed (byte* p = dest)
            {
                MemCopy(src, srcOffset, p, destOffset, count);
            }
        }

        private static void MemCopy(byte* src, long srcOffset, byte* dest, long destOffset, long count)
        {
            byte* psrc = src + srcOffset;
            byte* pdest = dest + destOffset;

            for (var i = 0; i < count; i++)
            {
                *pdest = *psrc;
                pdest++;
                psrc++;
            }
        }

        private void SeekInternal(long positionAligned, SeekOrigin origin)
        {
            NativeFile.Seek(_handle, positionAligned, origin);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long mungedOffset = offset;
            CheckDisposed();
            if (origin == SeekOrigin.Current) throw new NotImplementedException("only supports seek origin begin/end");
            if (origin == SeekOrigin.End) mungedOffset = Length + offset;
            var aligned = GetLowestAlignment(mungedOffset);
            var left = (int)(mungedOffset - aligned);

            _bufferedCount = left;
            _lastPosition = aligned;
            //TODO cant do two seeks + a read here.
            SeekInternal(aligned, SeekOrigin.Begin);
            NativeFile.Read(_handle, _readBuffer, 0, (int)_blockSize);
            SeekInternal(aligned, SeekOrigin.Begin);
            return offset;
        }

        private long GetLowestAlignment(long offset)
        {
            return offset - (offset % _blockSize);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            if (offset < 0 || buffer.Length < offset) throw new ArgumentException("offset");
            if (count < 0 || buffer.Length < count) throw new ArgumentException("offset");
            if (offset + count > buffer.Length)
                throw new ArgumentException("offset + count must be less than size of array");
            var position = GetLowestAlignment(Position);
            var roffset = (int)(Position - position);
            //var toRead = count > _readBufferSize ? _readBufferSize : count;
            var bytesRead = _readBufferSize;
            if (_readLocation + _readBufferSize <= position || _readLocation > position || _readLocation == -1)
            {
                SeekInternal(position, SeekOrigin.Begin);
                bytesRead = NativeFile.Read(_handle, _readBuffer, 0, _readBufferSize);
                _readLocation = position;
            }
            else if (_readLocation != position)
            {
                roffset += (int)(position - _readLocation);
            }

            var bytesAvailable = bytesRead - roffset;
            if (bytesAvailable <= 0) return 0;
            var toCopy = count > bytesAvailable ? bytesAvailable : count;

            MemCopy(_readBuffer, roffset, buffer, offset, toCopy);
            _bufferedCount += toCopy;
            if (count - toCopy == 0) return toCopy;
            return toCopy + Read(buffer, offset + toCopy, count - toCopy);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get
            {
                CheckDisposed();
                return NativeFile.GetFileSize(_handle);
            }
        }

        public override long Position
        {
            get
            {
                CheckDisposed();
                return GetLowestAlignment(_lastPosition) + _bufferedCount;
            }
            set
            {
                CheckDisposed();
                Seek(value, SeekOrigin.Begin);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void CheckDisposed()
        {
            //only check in debug
            if (_handle == null) throw new ObjectDisposedException("object is disposed.");
        }

        protected override void Dispose(bool disposing)
        {
            if (_handle == null) return;
            _handle.Close();
            _handle = null;
            _readBuffer = (byte*)IntPtr.Zero;
            Marshal.FreeHGlobal(_readBufferOriginal);
            GC.SuppressFinalize(this);
        }
    }
}
