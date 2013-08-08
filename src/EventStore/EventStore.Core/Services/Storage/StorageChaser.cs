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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage
{
    public class StorageChaser : IMonitoredQueue,
                                 IHandle<SystemMessage.SystemInit>,
                                 IHandle<SystemMessage.SystemStart>,
                                 IHandle<SystemMessage.BecomeShuttingDown>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageChaser>();

        private static readonly int TicksPerMs = (int)(Stopwatch.Frequency / 1000);
        private static readonly int MinFlushDelay = 2 * TicksPerMs;
        private static readonly TimeSpan FlushWaitTimeout = TimeSpan.FromMilliseconds(100);

        public string Name { get { return _queueStats.Name; } }

        private readonly IPublisher _masterBus;
        private readonly ICheckpoint _writerCheckpoint;
        private readonly ITransactionFileChaser _chaser;
        private readonly IReadIndex _readIndex;
        private readonly IEpochManager _epochManager;
        private Thread _thread;
        private volatile bool _stop;

        private readonly QueueStatsCollector _queueStats = new QueueStatsCollector("Storage Chaser");

        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _flushDelay;
        private long _lastFlush;

        public StorageChaser(IPublisher masterBus, 
                             ICheckpoint writerCheckpoint, 
                             ITransactionFileChaser chaser, 
                             IReadIndex readIndex, 
                             IEpochManager epochManager)
        {
            Ensure.NotNull(masterBus, "masterBus");
            Ensure.NotNull(chaser, "chaser");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(epochManager, "epochManager");

            _masterBus = masterBus;
            _writerCheckpoint = writerCheckpoint;
            _chaser = chaser;
            _readIndex = readIndex;
            _epochManager = epochManager;

            _flushDelay = 0;
            _lastFlush = _watch.ElapsedTicks;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _thread = new Thread(ChaseTransactionLog);
            _thread.IsBackground = true;
            _thread.Name = Name;
        }

        public void Handle(SystemMessage.SystemStart message)
        {
            _thread.Start();
        }

        private void ChaseTransactionLog()
        {
            _queueStats.Start();
            QueueMonitor.Default.Register(this);

            _chaser.Open();
            while (!_stop)
            {
                try
                {
                    ChaserIteration();
                }
                catch (Exception exc)
                {
                    Log.FatalException(exc, "Error in StorageChaser. SOMETHING VERY BAD HAPPENED. Terminating...");
                    _queueStats.EnterIdle();
                    _queueStats.ProcessingStarted<FaultedChaserState>(0);
                    Application.Exit(ExitCode.Error, "Error in StorageChaser. SOMETHING VERY BAD HAPPENED. Terminating...\nError: " + exc.Message);
                    while (!_stop)
                    {
                        Thread.Sleep(100);
                    }
                    _queueStats.ProcessingEnded(0);
                }
            }
            _chaser.Close();
            _masterBus.Publish(new SystemMessage.ServiceShutdown(Name));

            _queueStats.EnterIdle();
            _queueStats.Stop();
            QueueMonitor.Default.Unregister(this);
        }

        private void ChaserIteration()
        {
            _queueStats.EnterBusy();
            _queueStats.ProcessingStarted<ChaserTryReadNext>(0);
            var result = _chaser.TryReadNext();
            _queueStats.ProcessingEnded(1);

            if (result.Success)
            {
                _queueStats.ProcessingStarted(result.LogRecord.GetType(), 0);
                ProcessLogRecord(result);
                _queueStats.ProcessingEnded(1);
            }

            var start = _watch.ElapsedTicks;
            if (!result.Success || start - _lastFlush >= _flushDelay + MinFlushDelay)
            {
                _queueStats.ProcessingStarted<ChaserCheckpointFlush>(0);
                _chaser.Flush();
                _queueStats.ProcessingEnded(1);

                var end = _watch.ElapsedTicks;
                _flushDelay = end - start;
                _lastFlush = end;
            }

            if (!result.Success)
            {
                _queueStats.EnterIdle();
                //Thread.Sleep(1);
                _writerCheckpoint.WaitForFlush(FlushWaitTimeout);
            }
        }

        private void ProcessLogRecord(SeqReadResult result)
        {
            switch (result.LogRecord.RecordType)
            {
                case LogRecordType.Prepare:
                {
                    var record = (PrepareLogRecord) result.LogRecord;
                    if (record.Flags.HasAllOf(PrepareFlags.IsCommitted | PrepareFlags.TransactionEnd))
                    {
                        _masterBus.Publish(new StorageMessage.CommitAck(
                            record.CorrelationId, record.LogPosition, record.TransactionPosition,
                            record.ExpectedVersion + 1 - record.TransactionOffset));
                    }
                    else if (record.Flags.HasAnyOf(PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd))
                        _masterBus.Publish(new StorageMessage.PrepareAck(
                            record.CorrelationId, record.LogPosition, record.Flags));
                    break;
                }
                case LogRecordType.Commit:
                {
                    var record = (CommitLogRecord) result.LogRecord;
                    _masterBus.Publish(new StorageMessage.CommitAck(record.CorrelationId,
                                                                    record.LogPosition,
                                                                    record.TransactionPosition,
                                                                    record.FirstEventNumber));
                    _readIndex.Commit(record);
                    break;
                }
                case LogRecordType.System:
                {
                    var record = (SystemLogRecord) result.LogRecord;
                    if (record.SystemRecordType == SystemRecordType.Epoch)
                    {
                        // Epoch record is written to TF, but possibly is not added to EpochManager 
                        // as we could be in Slave\Clone mode. We try to add epoch to EpochManager 
                        // every time we encounter EpochRecord while chasing. SetLastEpoch call is idempotent, 
                        // but does integrity checks.
                        var epoch = record.GetEpochRecord();
                        _epochManager.SetLastEpoch(epoch);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {
            _stop = true;
        }

        public QueueStats GetStatistics()
        {
            return _queueStats.GetStatistics(0);
        }

        private class ChaserTryReadNext
        {
        }

        private class ChaserCheckpointFlush
        {
        }

        private class FaultedChaserState
        {
        }
    }
}