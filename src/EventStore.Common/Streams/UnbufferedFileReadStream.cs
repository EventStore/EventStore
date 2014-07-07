using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Common.Streams
{
#if ! __MonoCS__
    public class UnbufferedFileReadStream : Stream
    {
        private readonly uint _alignment;
        private readonly SafeFileHandle _fileHandle;
        private long _positionAligned;
        private int _positionOffset;
        private readonly byte[] _buffer;
        private int _dataBuffered;
        private bool _disposed;

        private UnbufferedFileReadStream(string filename)
        {
            _alignment = DriveSectorSize.GetDriveSectorSize(filename);
            _fileHandle = WinApi.CreateFile(filename, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, WinApi.FILE_FLAG_NO_BUFFERING | WinApi.FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);
            _buffer = new byte[DriveSectorSize.GetDriveSectorSize(filename) * 32];
        }

        public static UnbufferedFileReadStream Open(string filename)
        {
            return new UnbufferedFileReadStream(filename);
        }

        public override void Flush()
        {
            //do nothing
        }

        private long GetLowestAlignment(long offset)
        {
            return offset - (offset % _alignment);
        }

        public unsafe override long Seek(long offset, SeekOrigin origin)
        {
            if(origin != SeekOrigin.Begin) throw new NotImplementedException();
            _positionAligned = GetLowestAlignment(offset);
            _positionOffset = (int) (offset - _positionAligned);
            WinApi.SetFilePointer(_fileHandle, (int)_positionAligned, null, WinApi.EMoveMethod.Begin);
            ReadNative();
            return offset;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(_positionOffset + count > _dataBuffered)
            {
                var tocopy = _dataBuffered - _positionOffset;
                if(tocopy > 0)
                    Buffer.BlockCopy(_buffer,  _positionOffset, buffer, offset, tocopy);
                _dataBuffered = ReadNative();
                _positionOffset = 0;
                var left = count - tocopy;
                left = left > _dataBuffered ? _dataBuffered : left;
                if (left > 0)
                {
                    Buffer.BlockCopy(_buffer, _positionOffset, buffer, tocopy, left);
                    _positionOffset += left;
                }
                var total = left + tocopy;
                return total;
            }
            Buffer.BlockCopy(_buffer, _positionOffset, buffer, offset, count);
            _positionOffset += count;
            return count;
        }

        private unsafe int ReadNative()
        {
            int read = 0;
            fixed (byte* p = _buffer)
            {
                if (!WinApi.ReadFile(_fileHandle.DangerousGetHandle(), p, _buffer.Length, &read, 0))
                {
                    return 0;
                }
            }
            return read;
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
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get
            {
                long length;
                if(!WinApi.GetFileSizeEx(_fileHandle.DangerousGetHandle(), out length))
                {
                    throw new Win32Exception();
                }
                return length;
            }
        }

        public override long Position
        {
            get { return _positionAligned + _positionOffset; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        protected override void Dispose(bool disposing)
        {
            if(_disposed)
                return;

            if (!_fileHandle.IsClosed)
                _fileHandle.Close();

            _disposed = true;
            
            base.Dispose(disposing);
        }
    }

    public class NotAlignedReadException
        : Exception
    {
    }
#endif
}
