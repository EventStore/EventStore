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
using System.Text;
using System.Threading;
using EventStore.BufferManagement;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Storage
{
    public class StorageWriter : IDisposable,
                                 IHandle<Message>,
                                 IHandle<SystemMessage.SystemInit>,
                                 IHandle<SystemMessage.BecomeShuttingDown>,
                                 IHandle<ReplicationMessage.WritePrepares>,
                                 IHandle<ReplicationMessage.WriteDelete>,
                                 IHandle<ReplicationMessage.WriteTransactionStart>,
                                 IHandle<ReplicationMessage.WriteTransactionData>,
                                 IHandle<ReplicationMessage.WriteTransactionPrepare>,
                                 IHandle<ReplicationMessage.WriteCommit>,
                                 IHandle<ReplicationMessage.LogBulk>,
                                 IHandle<SystemMessage.StateChangeMessage>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageWriter>();
        private static readonly decimal MsPerTick = 1000.0M / Stopwatch.Frequency;

        private readonly ITransactionFileWriter _writer;
        private readonly IPublisher _bus;

        private QueuedHandler _storageWriterQueue;
        private readonly LengthPrefixMessageFramerWithBufferPool _framer = new LengthPrefixMessageFramerWithBufferPool();
        private readonly IReadIndex _readIndex;

        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _flushDelay;
        private long _lastFlush;
        private long _lastWriteCheck = -1;

        private int _flushMessagesInQueue;
        private VNodeState _state = VNodeState.Initializing;

        public StorageWriter(IPublisher bus, ISubscriber subscriber, ITransactionFileWriter writer, IReadIndex readIndex)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(subscriber, "subscriber");
            Ensure.NotNull(writer, "writer");
            Ensure.NotNull(readIndex, "readIndex");

            _bus = bus;
            _readIndex = readIndex;

            _flushDelay = 0;
            _lastFlush = _watch.ElapsedTicks;

            _writer = writer;
            _writer.Open();
            _framer.RegisterMessageArrivedCallback(OnLogRecordArrived);

            SetupMessaging(subscriber);
        }

        private void SetupMessaging(ISubscriber subscriber)
        {
            var storageWriterBus = new InMemoryBus("StorageWriterBus", watchSlowMsg: true, slowMsgThresholdMs: 500);
            storageWriterBus.Subscribe<SystemMessage.StateChangeMessage>(this);
            storageWriterBus.Subscribe<SystemMessage.SystemInit>(this);
            storageWriterBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WritePrepares>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WriteDelete>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WriteTransactionStart>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WriteTransactionData>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WriteTransactionPrepare>(this);
            storageWriterBus.Subscribe<ReplicationMessage.WriteCommit>(this);
            storageWriterBus.Subscribe<ReplicationMessage.LogBulk>(this);

            _storageWriterQueue = new QueuedHandler(storageWriterBus, "StorageWriterQueue", watchSlowMsg: false);
            _storageWriterQueue.Start();

            subscriber.Subscribe(this.WidenFrom<SystemMessage.StateChangeMessage, Message>());
            subscriber.Subscribe(this.WidenFrom<SystemMessage.SystemInit, Message>());
            subscriber.Subscribe(this.WidenFrom<SystemMessage.BecomeShuttingDown, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WritePrepares, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WriteDelete, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WriteTransactionStart, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WriteTransactionData, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WriteTransactionPrepare, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.WriteCommit, Message>());
            subscriber.Subscribe(this.WidenFrom<ReplicationMessage.LogBulk, Message>());
        }

        void IHandle<SystemMessage.StateChangeMessage>.Handle(SystemMessage.StateChangeMessage message)
        {
            _state = message.State;
        }

        void IHandle<Message>.Handle(Message message)
        {
            if (message is ReplicationMessage.IFlushableWriterMessage)
                Interlocked.Increment(ref _flushMessagesInQueue);

            _storageWriterQueue.Publish(message);

            // TODO AN manage this cyclic thread stopping dependency
            if (message is SystemMessage.BecomeShuttingDown)
            {
                _storageWriterQueue.Stop();
                _bus.Publish(new SystemMessage.ServiceShutdown("StorageWriter"));
            }
        }

        void IHandle<SystemMessage.SystemInit>.Handle(SystemMessage.SystemInit message)
        {
            _readIndex.Build();
            _bus.Publish(new SystemMessage.StorageWriterInitializationDone());
        }

        void IHandle<SystemMessage.BecomeShuttingDown>.Handle(SystemMessage.BecomeShuttingDown message)
        {
            Dispose();
        }

        void IHandle<ReplicationMessage.WritePrepares>.Handle(ReplicationMessage.WritePrepares message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);

            try
            {
                Debug.Assert(message.Events.Length > 0);

                var logPosition = _writer.Checkpoint.ReadNonFlushed();
                var transactionPosition = logPosition;

                var shouldCreateStream = ShouldCreateStreamFor(message);
                if (ShouldCreateStreamFor(message))
                {
                    var res = WritePrepareWithRetry(LogRecord.StreamCreated(logPosition,
                                                                            message.CorrelationId,
                                                                            transactionPosition,
                                                                            message.EventStreamId,
                                                                            LogRecord.NoData));
                    transactionPosition = res.WrittenPos;
                    logPosition = res.NewPos;
                }

                for (int i = 0; i < message.Events.Length; ++i)
                {
                    var evnt = message.Events[i];
                    var flags = PrepareFlags.Data;
                    if (i == 0 && !shouldCreateStream)
                        flags |= PrepareFlags.TransactionBegin;
                    if (i == message.Events.Length - 1)
                        flags |= PrepareFlags.TransactionEnd;
                    if (evnt.IsJson)
                        flags |= PrepareFlags.IsJson;

                    var expectedVersion = shouldCreateStream
                                              ? ExpectedVersion.Any
                                              : (i == 0 ? message.ExpectedVersion : ExpectedVersion.Any);
                    var res = WritePrepareWithRetry(LogRecord.Prepare(logPosition,
                                                                      message.CorrelationId,
                                                                      evnt.EventId,
                                                                      transactionPosition,
                                                                      message.EventStreamId,
                                                                      expectedVersion,
                                                                      flags,
                                                                      evnt.EventType,
                                                                      evnt.Data,
                                                                      evnt.Metadata));
                    logPosition = res.NewPos;
                }
            }
            finally
            {
                Flush();
            }
        }

        void IHandle<ReplicationMessage.WriteTransactionStart>.Handle(ReplicationMessage.WriteTransactionStart message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);

            try
            {
                var logPosition = _writer.Checkpoint.ReadNonFlushed();
                var record = ShouldCreateStreamFor(message)
                    ? LogRecord.StreamCreated(logPosition, message.CorrelationId, logPosition, message.EventStreamId, LogRecord.NoData)
                    : LogRecord.TransactionBegin(logPosition, message.CorrelationId, message.EventStreamId, message.ExpectedVersion);
                WritePrepareWithRetry(record);
            }
            finally
            {
                Flush();
            }
        }

        void IHandle<ReplicationMessage.WriteTransactionData>.Handle(ReplicationMessage.WriteTransactionData message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);
            try
            {
                Debug.Assert(message.Events.Length > 0);

                var logPosition = _writer.Checkpoint.ReadNonFlushed();
                for (int i = 0; i < message.Events.Length; ++i)
                {
                    var evnt = message.Events[i];
                    var record = LogRecord.TransactionWrite(logPosition,
                                                            message.CorrelationId,
                                                            evnt.EventId,
                                                            message.TransactionId,
                                                            message.EventStreamId,
                                                            evnt.EventType,
                                                            evnt.Data,
                                                            evnt.Metadata);
                    var res = WritePrepareWithRetry(record);
                    logPosition = res.NewPos;
                }
            }
            finally
            {
                Flush();
            }
        }

        void IHandle<ReplicationMessage.WriteTransactionPrepare>.Handle(ReplicationMessage.WriteTransactionPrepare message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);
            try
            {
                var record = LogRecord.TransactionEnd(_writer.Checkpoint.ReadNonFlushed(),
                                                      message.CorrelationId,
                                                      Guid.NewGuid(),
                                                      message.TransactionId,
                                                      message.EventStreamId);
                WritePrepareWithRetry(record);
            }
            finally
            {
                Flush();
            }
        }

        void IHandle<ReplicationMessage.WriteCommit>.Handle(ReplicationMessage.WriteCommit message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);
            try
            {
                var result = _readIndex.CheckCommitStartingAt(message.PrepareStartPosition);
                switch (result.Decision)
                {
                    case CommitDecision.Ok:
                    {
                        var commit = WriteCommitWithRetry(LogRecord.Commit(_writer.Checkpoint.ReadNonFlushed(),
                                                                           message.CorrelationId,
                                                                           message.PrepareStartPosition,
                                                                           result.CurrentVersion + 1));
                        _readIndex.Commit(commit);
                        break;
                    }
                    case CommitDecision.WrongExpectedVersion:
                        message.Envelope.ReplyWith(new ReplicationMessage.WrongExpectedVersion(message.CorrelationId));
                        break;
                    case CommitDecision.Deleted:
                        message.Envelope.ReplyWith(new ReplicationMessage.StreamDeleted(message.CorrelationId));
                        break;
                    case CommitDecision.Idempotent:
                        message.Envelope.ReplyWith(new ReplicationMessage.AlreadyCommitted(message.CorrelationId,
                                                                                           result.EventStreamId,
                                                                                           result.StartEventNumber,
                                                                                           result.EndEventNumber));
                        break;
                    case CommitDecision.CorruptedIdempotency:
                        //TODO AN add messages and error code for invalid idempotent request
                        throw new Exception("The request was partially committed and other part is different.");
                    case CommitDecision.InvalidTransaction:
                        message.Envelope.ReplyWith(new ReplicationMessage.InvalidTransaction(message.CorrelationId));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            finally
            {
                Flush();
            }
        }

        void IHandle<ReplicationMessage.WriteDelete>.Handle(ReplicationMessage.WriteDelete message)
        {
            if (_state != VNodeState.Master) throw new InvalidOperationException("Write request not in working state.");

            Interlocked.Decrement(ref _flushMessagesInQueue);
            try
            {
                if (ShouldCreateStreamFor(message))
                {
                    var transactionPos = _writer.Checkpoint.ReadNonFlushed();
                    var res = WritePrepareWithRetry(LogRecord.StreamCreated(transactionPos,
                                                                            message.CorrelationId,
                                                                            transactionPos,
                                                                            message.EventStreamId,
                                                                            LogRecord.NoData));
                    transactionPos = res.WrittenPos;

                    WritePrepareWithRetry(LogRecord.Prepare(res.NewPos,
                                                            message.CorrelationId,
                                                            Guid.NewGuid(),
                                                            transactionPos,
                                                            message.EventStreamId,
                                                            message.ExpectedVersion,
                                                            PrepareFlags.StreamDelete | PrepareFlags.TransactionEnd,
                                                            SystemEventTypes.StreamDeleted,
                                                            LogRecord.NoData,
                                                            LogRecord.NoData));
                }
                else
                {
                    var record = LogRecord.DeleteTombstone(_writer.Checkpoint.ReadNonFlushed(),
                                                           message.CorrelationId,
                                                           message.EventStreamId,
                                                           message.ExpectedVersion);
                    WritePrepareWithRetry(record);
                }
            }
            finally
            {
                Flush();
            }
        }

        private WriteResult WritePrepareWithRetry(PrepareLogRecord prepare)
        {
            long writtenPos = prepare.LogPosition;
            long newPos;
            if (!_writer.Write(prepare, out newPos))
            {
                var transactionPos = prepare.TransactionPosition == prepare.LogPosition ? newPos : prepare.TransactionPosition;
                var record = new PrepareLogRecord(newPos,
                                                  prepare.CorrelationId,
                                                  prepare.EventId,
                                                  transactionPos,
                                                  prepare.EventStreamId,
                                                  prepare.ExpectedVersion,
                                                  prepare.TimeStamp,
                                                  prepare.Flags,
                                                  prepare.EventType,
                                                  prepare.Data,
                                                  prepare.Metadata);
                writtenPos = newPos;
                if (!_writer.Write(record, out newPos))
                {
                    throw new Exception(string.Format("Second write try failed when first writing prepare at {0}, then at {1}.",
                                                      prepare.LogPosition,
                                                      writtenPos));
                }
            }
            return new WriteResult(writtenPos, newPos);
        }

        private CommitLogRecord WriteCommitWithRetry(CommitLogRecord commit)
        {
            long newPos;
            if (!_writer.Write(commit, out newPos))
            {
                var transactionPos = commit.TransactionPosition == commit.LogPosition ? newPos : commit.TransactionPosition;
                var record = new CommitLogRecord(newPos,
                                                 commit.CorrelationId,
                                                 transactionPos,
                                                 commit.TimeStamp,
                                                 commit.EventNumber);
                long writtenPos = newPos;
                if (!_writer.Write(record, out newPos))
                {
                    throw new Exception(string.Format("Second write try failed when first writing commit at {0}, then at {1}.",
                                                      commit.LogPosition,
                                                      writtenPos));
                }
                return record;
            }
            return commit;
        }

        private bool ShouldCreateStreamFor(ReplicationMessage.IPreconditionedWriteMessage message)
        {
            if (!message.AllowImplicitStreamCreation)
                return false;

            return message.ExpectedVersion == ExpectedVersion.NoStream
                   || (message.ExpectedVersion == ExpectedVersion.Any
                       && _readIndex.GetLastStreamEventNumber(message.EventStreamId) == ExpectedVersion.NoStream);
        }

        void IHandle<ReplicationMessage.LogBulk>.Handle(ReplicationMessage.LogBulk message)
        {
            Interlocked.Decrement(ref _flushMessagesInQueue);

            try
            {
                if (_lastWriteCheck != -1 && message.LogPosition != _lastWriteCheck)
                {
                    var checksum = _writer.Checkpoint.ReadNonFlushed();
                    Log.Debug("WRITER WARNING: Unexpected LogPosition: {0}, should be: {1}, writer checkpoint: {2}",
                              message.LogPosition,
                              _lastWriteCheck,
                              checksum);

                    if (message.LogPosition == checksum)
                    {
                        Log.Debug("WRITER OK: LogPosition is THE SAME as unflushed writer checkpoint.");
                        _framer.Reset();
                    }
                    else
                    {
                        Log.Debug("WRITER ERROR: LogPosition is NOT THE SAME as unflushed writer checkpoint. IGNORING...");
                        return;
                    }
                }

                _framer.UnFrameData(new ArraySegment<byte>(message.LogData));
                _lastWriteCheck = message.LogPosition + message.LogData.Length;
            }
            finally
            {
                Flush();
            }
        }

        private void OnLogRecordArrived(BufferPool bufferPool)
        {
            using (var reader = new BinaryReader(new BufferPoolStream(bufferPool)))
            {
                var record = LogRecord.ReadFrom(reader);
                switch(record.RecordType)
                {
                    case LogRecordType.Prepare:
                    {
                        WritePrepareWithRetry((PrepareLogRecord) record);
                        break;
                    }
                    case LogRecordType.Commit:
                    {
                        WriteCommitWithRetry((CommitLogRecord) record);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                bufferPool.Dispose();
            }
        }

        private void Flush()
        {
            if (ShouldForceFlush())
            {
                var start = _watch.ElapsedTicks;
                _writer.Flush();
                _flushDelay = _watch.ElapsedTicks - start;
                _lastFlush = _watch.ElapsedTicks;
            }
        }

        private bool ShouldForceFlush()
        {
            return _watch.ElapsedTicks - _lastFlush >= _flushDelay + 2 * MsPerTick || _flushMessagesInQueue == 0;
        }

        public void Dispose()
        {
            _writer.Flush();
            _writer.Close();

            _readIndex.Close();

            // TODO AN manage this cyclic thread stopping dependency
            //_storageWriterQueue.Stop();
        }

        private string FormatBinaryDump(byte[] logBulk)
        {
            if (logBulk == null || logBulk.Length == 0)
                return "--- NO DATA ---";

            var sb = new StringBuilder();
            int cur = 0;
            int len = logBulk.Length;
            for (int row = 0, rows = (logBulk.Length + 15) / 16; row < rows; ++row)
            {
                sb.AppendFormat("{0:000000}:", row * 16);
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur >= len)
                        sb.Append("   ");
                    else
                        sb.AppendFormat(" {0:X2}", logBulk[cur]);
                }
                sb.Append("  | ");
                cur -= 16;
                for (int i = 0; i < 16; ++i, ++cur)
                {
                    if (cur < len)
                        sb.Append(char.IsControl((char)logBulk[cur]) ? '.' : (char)logBulk[cur]);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private struct WriteResult
        {
            public readonly long WrittenPos;
            public readonly long NewPos;

            public WriteResult(long writtenPos, long newPos)
            {
                WrittenPos = writtenPos;
                NewPos = newPos;
            }
        }
    }
}