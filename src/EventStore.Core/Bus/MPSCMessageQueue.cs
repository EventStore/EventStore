using System;
using System.Runtime.InteropServices;
using System.Threading;
using EventStore.Core.Messaging;

namespace EventStore.Core.Bus
{
    /// <summary>
    /// A much better concurrent queue than <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> for multi producer single consumer scenarios.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public class MPSCMessageQueue
    {
        const int CacheLineSize = 64;
        const int Padding = CacheLineSize;
        const int MinimalSize = 1 << MinimalSizeLog;
        const int MinimalSizeLog = MaskShift*2;
        const int MaskShift = 8;
        const int MaskLow = 0x000000FF;
        const int MaskHigh = 0x0000FF00;
        const int MaskUnchanged = ~0 ^ (MaskLow | MaskHigh);

        private struct MessageItem
        {
            public volatile Message Item;
        }

        [FieldOffset(Padding + CacheLineSize*0)] private readonly MessageItem[] array;

        [FieldOffset(Padding + CacheLineSize*1)] private readonly int count;

        [FieldOffset(Padding + CacheLineSize*2)] private long sequence;

        [FieldOffset(Padding + CacheLineSize*3)] private long sequenceReadTo;
        [FieldOffset(Padding + CacheLineSize*4)] private long sequenceReadToCache;

#pragma warning disable 414
        [FieldOffset(Padding + CacheLineSize*5)] private readonly long padding;
#pragma warning restore 414

        public MPSCMessageQueue(int size)
        {
            if (IsPowerOf2(size) == false)
            {
                throw new ArgumentException("Use only sizes equal power of 2");
            }
            if (size < MinimalSize)
            {
                throw new ArgumentException("The size should be at least " + MinimalSize);
            }

            array = new MessageItem[size];
            count = size;
            sequence = 0;
            sequenceReadTo = 0;
            sequenceReadToCache = 0;
            padding = 0;
        }

        public void Enqueue(Message item)
        {
            var next = Interlocked.Increment(ref sequence) - 1;
            var index = Map((int) (next & (count - 1)));

            do
            {
                // Volatile.Read(ref sequenceReadToCache);
                var readTo = sequenceReadToCache;
                Thread.MemoryBarrier();

                if (next - readTo < count)
                {
                    array[index].Item = item;
                    return;
                }

                // Volatile.Read(ref sequenceReadTo);
                readTo = sequenceReadTo;

                // Volatile.Write(ref sequenceReadToCache, readTo);
                Thread.MemoryBarrier();
                sequenceReadToCache = readTo;

                if (next - readTo < count)
                {
                    // Volatile.Write(ref array[index], item);
                    array[index].Item = item;
                    return;
                }
            } while (true);
        }

        public int TryDequeue(Message[] segment)
        {
            var i = 0;
            var length = segment.Length;

            // only one reader, no need to Volatile.Read(ref sequenceReadTo);
            var current = sequenceReadTo;

            while (i < length)
            {
                var index = Map((int) (current & (count - 1)));
                var stored = array[index].Item;
                if (stored != null)
                {
                    segment[i] = stored;

                    // if Volatile.Write was available, nulls could be assigned without it as the sequenceReadTo is writtent with it ;
                    array[index].Item = null;
                    i += 1;
                    current += 1;
                }
                else
                {
                    // Volatile.Write(ref sequenceReadTo, current -1);
                    Thread.MemoryBarrier();
                    sequenceReadTo = current;

                    return i;
                }
            }

            // Volatile.Write(ref sequenceReadTo, current -1);
            Thread.MemoryBarrier();
            sequenceReadTo = current;

            return i;
        }

        private static int Map(int s)
        {
            var low = s & MaskLow;
            var high = s & MaskHigh;
            var unchanged = s & MaskUnchanged;

            return (low << MaskShift) | high >> MaskShift | unchanged;
        }

        private static bool IsPowerOf2(int n)
        {
            return (n & (n - 1)) == 0;
        }
    }
}