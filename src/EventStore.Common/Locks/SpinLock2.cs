using System;
using System.Threading;

namespace EventStore.Common.Locks
{
    public class SpinLock2
    {
        private int state;
        private readonly EventWaitHandle available = new AutoResetEvent(false);

        // This looks at the total number of hardware threads available; if it's
        // only 1, we will use an optimized code path
        private static bool isSingleProc = (Environment.ProcessorCount == 1);

        private const int outerTryCount = 5;
        private const int cexTryCount = 100;

        public void Enter(out bool taken)
        {
            // Taken is an out parameter so that we set it *inside* the critical
            // region, rather than returning it and permitting aborts to creep in.
            // Without this, the caller could take the lock, but not release it
            // because it didn't know it had to.
            taken = false;

            while (!taken)
            {
                if (isSingleProc)
                {
                    // Don't busy wait on 1-logical processor machines; try
                    // a single swap, and if it fails, drop back to EventWaitHandle.
                    Thread.BeginCriticalRegion();
                    taken = Interlocked.CompareExchange(ref state, 1, 0) == 0;
                    if (!taken)
                        Thread.EndCriticalRegion();
                }
                else
                {
                    for (int i = 0; !taken && i < outerTryCount; i++)
                    {
                        // Tell the CLR we're in a critical region;
                        // interrupting could lead to deadlocks.
                        Thread.BeginCriticalRegion();

                        // Try 'cexTryCount' times to CEX the state variable:
                        int tries = 0;
                        while (!(taken = Interlocked.CompareExchange(ref state, 1, 0) == 0) && tries++ < cexTryCount)
                        {
                            Thread.SpinWait(1);
                        }

                        if (!taken)
                        {
                            // We failed to acquire in the busy spin, mark the end
                            // of our critical region and yield to let another
                            // thread make forward progress.
                            Thread.EndCriticalRegion();
                            Thread.Sleep(0);
                        }
                    }
                }

                // If we didn't acquire the lock, block.
                if (!taken) available.WaitOne();
            }
        }

        public LockReleaserSlim Acquire()
        {
            bool taken;
            Enter(out taken);
            if (taken)
                return new LockReleaserSlim(this);
            throw new Exception("Unable to acquire lock, this shouldn't happen.");
        }

        public void Exit()
        {
            if (Interlocked.CompareExchange(ref state, 0, 1) == 1)
            { 
                // We notify the waking threads inside our critical region so
                // that an abort doesn't cause us to lose a pulse, (which could
                // lead to deadlocks).
                available.Set();
                Thread.EndCriticalRegion();
            }
        }
    }

    // BE VERY CAREFUL USING THIS!!!
    public struct LockReleaserSlim: IDisposable
    {
        private readonly SpinLock2 _spinLock;

        public LockReleaserSlim(SpinLock2 spinLock)
        {
            _spinLock = spinLock;
        }

        public void Dispose()
        {
            _spinLock.Exit();
        }
    }
}