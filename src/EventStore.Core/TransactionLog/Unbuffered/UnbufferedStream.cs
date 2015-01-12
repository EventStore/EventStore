
using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Unbuffered
{
    public class UnbufferedIOFileStream : Stream
    {
        private readonly byte[] _writeBuffer;
        private readonly int _blockSize;
        private int _bufferedCount;
        private bool _aligned;
        private long _lastPosition;
        private bool _needsFlush;
        private SafeFileHandle _handle;
        private readonly byte [] _readBuffer;
        private int _readLocation;

        private UnbufferedIOFileStream(SafeFileHandle handle, int blockSize, int internalWriteBufferSize, int internalReadBufferSize)
        {
            _handle = handle;
            _writeBuffer = new byte[internalWriteBufferSize];
            _readBuffer = new byte[internalReadBufferSize];
            _blockSize = blockSize;
        }

        public static UnbufferedIOFileStream Create(string path,
            FileMode mode,
            FileAccess acc,
            FileShare share,
            bool sequential,
            int internalWriteBufferSize,
            int internalReadBufferSize,
            bool writeThrough,
            uint minBlockSize)
        {
            var blockSize = NativeFile.GetDriveSectorSize(path);
            blockSize = blockSize > minBlockSize ? blockSize : minBlockSize;
            if (internalWriteBufferSize%blockSize != 0)
                throw new Exception("write buffer size must be aligned to block size of " + blockSize + " bytes");
            if (internalReadBufferSize % blockSize != 0)
                throw new Exception("read buffer size must be aligned to block size of " + blockSize + " bytes");
            var flags = ExtendedFileOptions.NoBuffering;
            if (writeThrough) flags = flags | ExtendedFileOptions.WriteThrough;

            var handle = NativeFile.CreateUnbufferedRW(path,FileMode.Create);
            return new UnbufferedIOFileStream(handle, (int) blockSize, internalWriteBufferSize, internalReadBufferSize);
        }

        public override void Flush()
        {
            CheckDisposed();
            if (!_needsFlush) return;
            var alignedbuffer = (int) GetLowestAlignment(_bufferedCount);
            var positionAligned = GetLowestAlignment(_lastPosition);
            if (!_aligned)
            {
                SeekInternal(positionAligned);
            }
            if (_bufferedCount == alignedbuffer)
            {
                InternalWrite(_writeBuffer, (uint) _bufferedCount);
                _lastPosition = positionAligned + _bufferedCount;
                _bufferedCount = 0;
                _aligned = true;
            }
            else
            {
                var left = _bufferedCount - alignedbuffer;

                InternalWrite(_writeBuffer, (uint) (alignedbuffer + _blockSize));
                _lastPosition = positionAligned + alignedbuffer + left;
                SetBuffer(alignedbuffer, left);
                _bufferedCount = left;
                _aligned = false;
            }
            _needsFlush = false;
        }

        private void SeekInternal(long positionAligned)
        {
            NativeFile.Seek(_handle, (int) positionAligned, SeekOrigin.Begin);
        }

        private void InternalWrite(byte[] buffer, uint count)
        {
            var written = 0;
            NativeFile.Write(_handle, buffer, count, ref written);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();
            if(origin != SeekOrigin.Begin) throw new NotImplementedException("only supports seek origin begin");
            var aligned = GetLowestAlignment(offset);
            var left = (int) (offset - aligned);
            Flush();
            _bufferedCount = left;
            _aligned = aligned == left;
            _lastPosition = aligned;
            return offset;
        }

        private long GetLowestAlignment(long offset)
        {
            return offset - (offset%_blockSize);
        }

        public override void SetLength(long value)
        {
            CheckDisposed();
            var aligned = GetLowestAlignment(value);
            aligned = aligned == value ? aligned : aligned + _blockSize;
            NativeFile.SetFileSize(_handle, aligned);
            Seek(0, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            if (offset < 0 || buffer.Length < offset) throw new ArgumentException("offset");
            if (count < 0 || buffer.Length < count) throw new ArgumentException("offset");
            if (offset + count > buffer.Length)
                throw new ArgumentException("offset + count must be less than size of array");
            var position = (int) GetLowestAlignment(Position);
            var roffset = (int) (Position - position);

            var bytesRead = _readBuffer.Length;

            if(_readLocation + _readBuffer.Length < position || _readLocation > position || _readLocation == 0) {
                SeekInternal(position);
                bytesRead = NativeFile.Read(_handle, _readBuffer, 0, _readBuffer.Length);
                _readLocation = position;
            }

            var bytesAvailable = bytesRead - roffset;
            if (bytesAvailable <= 0) return 0;
            var toCopy = count > bytesAvailable ? bytesAvailable : count;

            Buffer.BlockCopy(_readBuffer, roffset, buffer,offset,toCopy);
            _bufferedCount += toCopy;
            return toCopy;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            var done = false;
            var left = count;
            var current = offset;
            while (!done)
            {
                _needsFlush = true;
                if (_bufferedCount + left < _writeBuffer.Length)
                {
                    CopyBuffer(buffer, current, left);
                    done = true;
                    current += left;
                }
                else
                {
                    var toFill = _writeBuffer.Length - _bufferedCount;
                    CopyBuffer(buffer, current, toFill);
                    Flush();
                    left -= toFill;
                    current += toFill;
                    done = left == 0;
                }
            }
        }

        private void CopyBuffer(byte[] buffer, int offset, int count)
        {
            Buffer.BlockCopy(buffer, offset, _writeBuffer, _bufferedCount, count);
            _bufferedCount += count;
        }

        public override bool CanRead
        {
            get 
            { 
                CheckDisposed();
                return true; 
            }
        }

        public override bool CanSeek
        {
            get 
            {
                CheckDisposed(); 
                return true; 
            }
        }

        public override bool CanWrite
        {
            get 
            { 
                CheckDisposed();
                return true; 
            }
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
                if (_aligned)
                    return _lastPosition + _bufferedCount;
                return GetLowestAlignment(_lastPosition) + _bufferedCount;
            }
            set 
            {
                CheckDisposed(); 
                Seek(value, SeekOrigin.Begin); 
            }
        }

        private void SetBuffer(int alignedbuffer, int left)
        {
            Buffer.BlockCopy(_writeBuffer, alignedbuffer, _writeBuffer, 0, left);
        }

        private void CheckDisposed() {
            if(_handle == null) throw new ObjectDisposedException("object is disposed.");
        }

        protected override void Dispose(bool disposing)
        {
            if(_handle == null) return;
            Flush();
            _handle.Close();
            _handle.Dispose();
            _handle = null;
            GC.SuppressFinalize (this);
            
        }
    }
}