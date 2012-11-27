// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

/*
Quote from

Professional .NET Framework 2.0 (Programmer to Programmer) (Paperback)
by Joe Duffy (Author)


# Paperback: 601 pages
# Publisher: Wrox (April 10, 2006)
# Language: English
# ISBN-10: 0764571354
# ISBN-13: 978-0764571350

*/

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

        public IDisposable Acquire()
        {
            bool taken;
            Enter(out taken);
            if(taken) return new LockReleaser(this);
            throw new Exception("Unable to acquire lock, this shouldnt happen.");
        }

        public void Enter()
        {
            // Convenience method. Using this could be prone to deadlocks.
            bool b;
            Enter(out b);
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

    internal class LockReleaser : IDisposable
    {
        private readonly SpinLock2 _spinLock2;
        private bool _disposed = false;

        public LockReleaser(SpinLock2 spinLock2)
        {
            _spinLock2 = spinLock2;
        }

        private void Dispose(bool disposing)
        {
            if(!disposing && !_disposed) throw new Exception("Lock is being finalized without being released!");
            if (_disposed) throw new Exception("Lock already disposed!");
            _spinLock2.Exit();
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~LockReleaser()
        {
            Dispose(false);
        }
    }
}