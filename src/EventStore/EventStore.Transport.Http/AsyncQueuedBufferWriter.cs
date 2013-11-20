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
using EventStore.Common.Concurrent;

namespace EventStore.Transport.Http
{
    /// <summary>
    /// Manages a queue of buffers to send to output stream and 
    /// sens them to the output stream as previous requests complete
    /// </summary>
    public class AsyncQueuedBufferWriter: IDisposable
    {
        private class Item
        {
            public readonly byte[] Buffer;
            public readonly Action<Exception> OnCompletion;

            public Item(byte[] buffer, Action<Exception> onCompletion)
            {
                Buffer = buffer;
                OnCompletion = onCompletion;
            }
        }

        private readonly Stream _outputStream;
        private readonly Action _onDispose;
        private readonly ConcurrentQueue<Item> _queue = new ConcurrentQueue<Item>();

        private int _processing;
        private Exception _error;
        private int _disposed;

        /// <param name="outputStream">NOTE: outputStream is NOT auto-disposed.</param>
        /// <param name="onDispose">Use to dispose response streams and close connections.</param>
        public AsyncQueuedBufferWriter(Stream outputStream, Action onDispose)
        {
            _outputStream = outputStream;
            _onDispose = onDispose;
        }

        public void Append(byte[] buffer, Action<Exception> onCompletion)
        {
            var item = new Item(buffer, onCompletion);
            _queue.Enqueue(item);

            if (Interlocked.CompareExchange(ref _processing, 1, 0) == 0)
                BeginProcessing();
        }

        /// <summary>
        /// Schedules auto-dispose when all previous writes are completed
        /// </summary>
        /// <param name="onCompletion">onCompletion handler is called after the object has been disposed</param>
        public void AppendDispose(Action<Exception> onCompletion)
        {
            var item = new Item(null, onCompletion);
            _queue.Enqueue(item);

            if (Interlocked.CompareExchange(ref _processing, 1, 0) == 0)
                BeginProcessing();
        }

        private void BeginProcessing()
        {
            if (_processing != 1)
                throw new InvalidOperationException();
            ContinueWriteOrStop();
        }

        private void ContinueWriteOrStop()
        {
            bool proceed = true;
            while (proceed)
            {
                Item item;
                if (_queue.TryDequeue(out item))
                {
                    try
                    {
                        if (item.Buffer != null)
                            _outputStream.BeginWrite(item.Buffer, 0, item.Buffer.Length, WriteCompleted, item);
                        else
                        {
                            Dispose();
                            if (item.OnCompletion != null) 
                                item.OnCompletion(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        _error = ex;
                        Dispose();
                    }
                    return;
                }

                Interlocked.Exchange(ref _processing, 0);
                proceed = _queue.Count > 0 && Interlocked.CompareExchange(ref _processing, 1, 0) == 0;
            }
        }

        private void WriteCompleted(IAsyncResult ar)
        {
            var item = (Item) ar.AsyncState;
            EndWrite(ar);
            RaiseCompletion(item);
            ContinueWriteOrStop();
        }

        private void EndWrite(IAsyncResult ar)
        {
            try
            {
                _outputStream.EndWrite(ar);
            }
            catch (Exception ex)
            {
                _error = ex;
                Dispose();
            }
        }

        private void RaiseCompletion(Item item)
        {
            try
            {
                if (item.OnCompletion != null)
                    item.OnCompletion(_error);
            }
            catch (Exception ex)
            {
                _error = ex;
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;
            if (_onDispose != null)
                _onDispose();
        }
    }
}
