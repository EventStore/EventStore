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
    public class MultiProducerSinglerConsumerConcurrentQueue
    {
        const int MinimalSize = 4096;
        const int MinimalSizeLog = 12;
        const int MaskSize = MinimalSizeLog/2;
        const int MaskLow = (1 << (MaskSize + 1)) - 1;
        const int MaskHigh = MaskLow << MaskSize;
        const int MaskUnchanged = ~0 ^ (MaskLow | MaskHigh);

        [FieldOffset(0)] private readonly Message[] array;

        [FieldOffset(32)] private readonly int count;

        [FieldOffset(64)] private long sequence;

        [FieldOffset(128)] private long sequenceReadTo;

        public MultiProducerSinglerConsumerConcurrentQueue(int size)
        {
            if (IsPowerOf2(size) == false)
            {
                throw new ArgumentException("Use only sizes equal power of 2");
            }
            if (size < MinimalSize)
            {
                throw new ArgumentException("The size should be at least " + MinimalSize);
            }

            array = new Message[size];
            count = size;
            sequence = 0;
            sequenceReadTo = 0;
        }

        public void Enqueue(Message item)
        {
            var next = Interlocked.Increment(ref sequence);
            var index = Map((int) (next & (count - 1)));

            if (Interlocked.CompareExchange(ref array[index], item, null) == null)
            {
                return;
            }

            var wait = new SpinWait();
            while (Interlocked.CompareExchange(ref array[index], item, null) == null)
            {
                wait.SpinOnce();
            }
        }

        public int TryDequeue(Message[] segment)
        {
            var i = 0;
            var length = segment.Length;
            var current = sequenceReadTo;

            while (i < length)
            {
                current += 1;
                var index = Map((int) (current & (count - 1)));
                var stored = Interlocked.Exchange(ref array[index], null);
                if (stored != null)
                {
                    segment[i] = stored;
                    i += 1;
                }
                else
                {
                    sequenceReadTo += i;
                    return i;
                }
            }

            return 0;
        }

        private static int Map(int s)
        {
            var low = s & MaskLow;
            var high = s & MaskHigh;
            var unchanged = s & MaskUnchanged;

            return (low << MaskSize) | high >> MaskSize | unchanged;
        }

        private static bool IsPowerOf2(int n)
        {
            return (n & (n - 1)) == 0;
        }
    }
}