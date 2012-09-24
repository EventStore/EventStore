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
// 
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http
{
    public class AsyncStreamCopier<T>
    {
        private static readonly ILogger Log = LogManager.GetLogger("AsyncStreamCopier");

        public event EventHandler Completed;

        public T AsyncState { get; private set; }
        public Exception Error { get; private set; }

        private readonly byte[] _buffer = new byte[4096];
        private readonly Stream _input;
        private readonly Stream _output;

        private readonly string _debuginfo;
        private readonly Stopwatch _watch = new Stopwatch();
        public AsyncStreamCopier(Stream input, Stream output, T state, string debuginfo = null)
        {
            Ensure.NotNull(input, "input");
            Ensure.NotNull(output, "output");

            _input = input;
            _output = output;

            AsyncState = state;
            Error = null;

            _debuginfo = debuginfo;
        }

        public void Start()
        {
            _watch.Start();
#if POOLCOPY
            ThreadPool.QueueUserWorkItem(_ =>
                                             {
                                                 try
                                                 {
                                                     _input.CopyTo(_output);
                                                     OnCompleted();
                                                 }
                                                 catch (Exception e)
                                                 {
                                                     Log.ErrorException(e, "CopyTo thrown an exception");
                                                     Error = e;
                                                     OnCompleted();
                                                 }
                                             });
#else
            GetNextChunk();
#endif
        }

        private void GetNextChunk()
        {
            try
            {
                _input.BeginRead(_buffer, 0, _buffer.Length, InputReadCompleted, null);
            }
            catch (Exception e)
            {
                Error = e;
                OnCompleted();
            }
        }

        private void InputReadCompleted(IAsyncResult ar)
        {
            try
            {
                int bytesRead = _input.EndRead(ar);
                if(ar.CompletedSynchronously)
                {
                    Log.Info("Completed synchronously. IN : {0}, OUT : {1}, DEBUGINFO : {2}",
                             _input.GetType().FullName,
                             _output.GetType().FullName,
                             _debuginfo);
                }
                if (bytesRead == 0 || bytesRead == -1) //TODO TR: only mono returns -1
                {
                    OnCompleted();
                    return;
                }

                _output.BeginWrite(_buffer, 0, bytesRead, OutputWriteCompleted, null);
            }
            catch (Exception e)
            {
                Error = e;
                OnCompleted();
            }
        }

        private void OutputWriteCompleted(IAsyncResult ar)
        {
            try
            {
                _output.EndWrite(ar);
                GetNextChunk();
            }
            catch (Exception e)
            {
                Error = e;
                OnCompleted();
            }
        }

        private void OnCompleted()
        {
            _watch.Stop();
            if (_watch.ElapsedMilliseconds > 5)
                Log.Info("Slow copy. IN : {0}, OUT : {1}, DEBUGINFO : {2}, TOOK {3}ms",
                         _input.GetType().FullName,
                         _output.GetType().FullName,
                         _debuginfo,
                         _watch.ElapsedMilliseconds);
            if (Completed != null)
                Completed(this, EventArgs.Empty);
        }
    }
}