using System;
using EventStore.Native;

namespace EventStore.Native.FileAccess.Tests
{
    public static unsafe class TestUtil
    {
        internal static NativeMethods.PageAlignedBuffer GetRndBuffer(long length)
        {
            var pages = length / NativeMethods.PageSize;
            if (length % NativeMethods.PageSize != 0) { pages++; }
            var buffer = NativeMethods.GetPageBuffer((int)pages);
            var rndSource = new byte[length];

            new Random(DateTime.Now.Millisecond).NextBytes(rndSource);
            fixed (byte* rnd = rndSource)
            {
                NativeMethods.Copy(buffer.Buffer, (IntPtr)rnd, length);
            }
            return buffer;
        }
        internal static NativeMethods.PageAlignedBuffer GetFilledBuffer(int length, byte fill)
        {
            var pages = length / NativeMethods.PageSize;
            if (length % NativeMethods.PageSize != 0) { pages++; }

            var buffer = NativeMethods.GetPageBuffer((int)pages);
            NativeMethods.Fill(buffer.Buffer, fill, (int)length);
            return buffer;
        }
        public static void ClearBuffer(this IntPtr buffer, int length)
        {
            NativeMethods.Clear(buffer, length);
        }
        public static void CopyTo(this IntPtr buffer, byte[] target, long length)
        {
            fixed (byte* p = target)
            {
                NativeMethods.Copy((IntPtr)p, buffer, (uint)length);
            }
        }

        public static bool BufferEqual(IntPtr p1, IntPtr p2, int length)
        {
            return NativeMethods.Compare(p1, p2, length) == 0;
        }
    }
}
