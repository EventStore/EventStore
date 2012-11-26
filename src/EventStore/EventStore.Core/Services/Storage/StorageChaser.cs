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
using System.Net;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage
{
    public class StorageChaser : IHandle<SystemMessage.SystemInit>,
                                 IHandle<SystemMessage.SystemStart>,
                                 IHandle<SystemMessage.BecomeShuttingDown>
    {
        private static readonly decimal MsPerTick = 1000.0M / Stopwatch.Frequency;

        private readonly IPublisher _masterBus;
        private readonly ITransactionFileChaser _chaser;
        private readonly IReadIndex _readIndex;
        private readonly IPEndPoint _vnodeEndPoint;
        private Thread _thread;
        private volatile bool _stop;

        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _flushDelay;
        private long _lastFlush;

        public StorageChaser(IPublisher masterBus, ITransactionFileChaser chaser, IReadIndex readIndex, IPEndPoint vnodeEndPoint)
        {
            Ensure.NotNull(masterBus, "masterBus");
            Ensure.NotNull(chaser, "chaser");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(vnodeEndPoint, "vnodeEndPoint");

            _masterBus = masterBus;
            _chaser = chaser;
            _readIndex = readIndex;
            _vnodeEndPoint = vnodeEndPoint;

            _flushDelay = 0;
            _lastFlush = _watch.ElapsedTicks;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _thread = new Thread(ChaseTransactionLog);
            _thread.IsBackground = true;
            _thread.Name = "StorageChaser";
        }

        public void Handle(SystemMessage.SystemStart message)
        {
            _thread.Start();
        }

        private void ChaseTransactionLog()
        {
            _chaser.Open();
            while (!_stop)
            {
                var result = _chaser.TryReadNext();
                if (result.Success)
                {
                    switch (result.LogRecord.RecordType)
                    {
                        case LogRecordType.Prepare:
                        {
                            var record = (PrepareLogRecord) result.LogRecord;

                            if ((record.Flags & PrepareFlags.TransactionBegin) != 0 
                                || (record.Flags & PrepareFlags.TransactionEnd) != 0)
                            {
                                _masterBus.Publish(new StorageMessage.PrepareAck(record.CorrelationId,
                                                                                 _vnodeEndPoint,
                                                                                 record.LogPosition,
                                                                                 record.Flags));
                            }

                            break;
                        }
                        case LogRecordType.Commit:
                        {
                            var record = (CommitLogRecord) result.LogRecord;
                            _masterBus.Publish(new StorageMessage.CommitAck(record.CorrelationId, 
                                                                            _vnodeEndPoint,
                                                                            record.LogPosition,
                                                                            record.TransactionPosition,
                                                                            record.EventNumber));
                            _readIndex.Commit(record);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (_watch.ElapsedTicks - _lastFlush >= _flushDelay + 2 * MsPerTick)
                {
                    var start = _watch.ElapsedTicks;
                    _chaser.Flush();
                    _flushDelay = _watch.ElapsedTicks - start;
                    _lastFlush = _watch.ElapsedTicks;
                }

                if (!result.Success)
                {
                    Thread.Sleep(1);
                }
            }
            _chaser.Close();
            _masterBus.Publish(new SystemMessage.ServiceShutdown("StorageChaser"));
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _stop = true;
        }
    }
}