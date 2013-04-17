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
using System.Text;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Checkpoint;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.Storage
{
    public class StorageReaderWorker: IHandle<ClientMessage.ReadEvent>,
                                      IHandle<ClientMessage.ReadStreamEventsBackward>,
                                      IHandle<ClientMessage.ReadStreamEventsForward>,
                                      IHandle<ClientMessage.ReadAllEventsForward>,
                                      IHandle<ClientMessage.ReadAllEventsBackward>,
                                      IHandle<StorageMessage.CheckStreamAccess>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageReaderWorker>();

        private readonly IReadIndex _readIndex;
        private readonly ICheckpoint _writerCheckpoint;

        public StorageReaderWorker(IReadIndex readIndex, ICheckpoint writerCheckpoint)
        {
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _readIndex = readIndex;
            _writerCheckpoint = writerCheckpoint;
        }

        void IHandle<ClientMessage.ReadEvent>.Handle(ClientMessage.ReadEvent msg)
        {
            var accessCheck = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
            switch (accessCheck)
            {
                case StreamAccessResult.Granted:
                {
                    var result = _readIndex.ReadEvent(msg.EventStreamId, msg.EventNumber);
                    var record = result.Result == ReadEventResult.Success && msg.ResolveLinkTos
                                         ? ResolveLinkToEvent(result.Record)
                                         : new ResolvedEvent(result.Record);
                    msg.Envelope.ReplyWith(new ClientMessage.ReadEventCompleted(
                        msg.CorrelationId, msg.EventStreamId, result.Result, record));
                    break;
                }
                case StreamAccessResult.Denied:
                {
                    msg.Envelope.ReplyWith(new ClientMessage.ReadEventCompleted(
                        msg.CorrelationId, msg.EventStreamId, ReadEventResult.AccessDenied, new ResolvedEvent(null)));
                    break;
                }
                default: throw new Exception(string.Format("Not expected StreamAccessResult '{0}'.", accessCheck));
            }
        }

        void IHandle<ClientMessage.ReadStreamEventsForward>.Handle(ClientMessage.ReadStreamEventsForward msg)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                var accessCheck = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
                switch (accessCheck)
                {
                    case StreamAccessResult.Granted:
                    {
                        if (msg.ValidationStreamVersion.HasValue && _readIndex.GetLastStreamEventNumber(msg.EventStreamId) == msg.ValidationStreamVersion)
                        {
                            Reply(msg, ReadStreamResult.NotModified, lastCommitPosition);
                        }
                        else
                        {
                            var result = _readIndex.ReadStreamEventsForward(msg.EventStreamId, msg.FromEventNumber, msg.MaxCount);
                            CheckEventsOrder(msg, result);
                            var resolvedPairs = ResolveLinkToEvents(result.Records, msg.ResolveLinks);
                            msg.Envelope.ReplyWith(new ClientMessage.ReadStreamEventsForwardCompleted(
                                msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
                                (ReadStreamResult)result.Result, resolvedPairs, string.Empty, 
                                result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastCommitPosition));
                        }
                        break;
                    }
                    case StreamAccessResult.Denied:
                    {
                        Reply(msg, ReadStreamResult.AccessDenied, lastCommitPosition); 
                        break;
                    }
                    default: throw new Exception(string.Format("Not expected StreamAccessResult '{0}'.", accessCheck));
                }
            }
            catch (Exception exc)
            {
                Reply(msg, ReadStreamResult.Error, lastCommitPosition, exc.Message);
                Log.ErrorException(exc, "Error during processing ReadStreamEventsForward request.");
            }
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward msg)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                var accessCheck = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
                switch (accessCheck)
                {
                    case StreamAccessResult.Granted:
                    {
                        if (msg.ValidationStreamVersion.HasValue && _readIndex.GetLastStreamEventNumber(msg.EventStreamId) == msg.ValidationStreamVersion)
                        {
                            Reply(msg, ReadStreamResult.NotModified, lastCommitPosition);
                        }
                        else
                        {
                            var result = _readIndex.ReadStreamEventsBackward(msg.EventStreamId, msg.FromEventNumber, msg.MaxCount);
                            CheckEventsOrder(msg, result);
                            var resolvedPairs = ResolveLinkToEvents(result.Records, msg.ResolveLinks);
                            msg.Envelope.ReplyWith(new ClientMessage.ReadStreamEventsBackwardCompleted(
                                msg.CorrelationId, msg.EventStreamId, result.FromEventNumber, result.MaxCount,
                                (ReadStreamResult)result.Result, resolvedPairs, string.Empty, 
                                result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastCommitPosition));
                        }
                        break;
                    }
                    case StreamAccessResult.Denied:
                    {
                        Reply(msg, ReadStreamResult.AccessDenied, lastCommitPosition);
                        break;
                    }
                    default: throw new Exception(string.Format("Not expected StreamAccessResult '{0}'.", accessCheck));
                }
            }
            catch (Exception exc)
            {
                Reply(msg, ReadStreamResult.Error, lastCommitPosition, exc.Message);
                Log.ErrorException(exc, "Error during processing ReadStreamEventsForward request.");
            }
        }

        void IHandle<ClientMessage.ReadAllEventsForward>.Handle(ClientMessage.ReadAllEventsForward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
            {
                var r = new ReadAllResult(ResolvedEvent.EmptyArray, message.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, r, notModified: false));
                return;
            }
            if (message.ValidationTfEofPosition.HasValue && _readIndex.LastCommitPosition == message.ValidationTfEofPosition.Value)
            {
                var r = new ReadAllResult(ResolvedEvent.EmptyArray, message.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, r, notModified: true));
                return;
            }
            var res = _readIndex.ReadAllEventsForward(pos, message.MaxCount);
            var result = ResolveReadAllResult(res, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, result, notModified: false));
        }

        void IHandle<ClientMessage.ReadAllEventsBackward>.Handle(ClientMessage.ReadAllEventsBackward message)
        {
            var pos = new TFPos(message.CommitPosition, message.PreparePosition);
            if (pos == TFPos.HeadOfTf)
            {
                var checkpoint = _writerCheckpoint.Read();
                pos = new TFPos(checkpoint, checkpoint);
            }
            if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
            {
                var r = new ReadAllResult(ResolvedEvent.EmptyArray, message.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsForwardCompleted(message.CorrelationId, r, notModified: false));
                return;
            }
            if (message.ValidationTfEofPosition.HasValue && _readIndex.LastCommitPosition == message.ValidationTfEofPosition.Value)
            {
                var r = new ReadAllResult(ResolvedEvent.EmptyArray, message.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, _writerCheckpoint.Read());
                message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsBackwardCompleted(message.CorrelationId, r, notModified: true));
                return;
            }
            var res = _readIndex.ReadAllEventsBackward(pos, message.MaxCount);
            var result = ResolveReadAllResult(res, message.ResolveLinks);
            message.Envelope.ReplyWith(new ClientMessage.ReadAllEventsBackwardCompleted(message.CorrelationId, result, notModified: false));
        }

        void IHandle<StorageMessage.CheckStreamAccess>.Handle(StorageMessage.CheckStreamAccess message)
        {
            var result = _readIndex.CheckStreamAccess(message.EventStreamId, message.AccessType, message.User);
            message.Envelope.ReplyWith(new StorageMessage.CheckStreamAccessCompleted(
                message.CorrelationId, message.EventStreamId, message.AccessType, result));
        }

        private static void Reply(ClientMessage.ReadStreamEventsForward msg, ReadStreamResult result, long lastCommitPosition, string error = null)
        {
            msg.Envelope.ReplyWith(ClientMessage.ReadStreamEventsForwardCompleted.NoRecords(
                result, msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, lastCommitPosition, error));
        }

        private static void Reply(ClientMessage.ReadStreamEventsBackward msg, ReadStreamResult result, long lastCommitPosition, string error = null)
        {
            msg.Envelope.ReplyWith(ClientMessage.ReadStreamEventsBackwardCompleted.NoRecords(
                result, msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, lastCommitPosition, error));
        }

        private static void CheckEventsOrder(ClientMessage.ReadStreamEventsForward msg, IndexReadStreamResult result)
        {
            for (var index = 1; index < result.Records.Length; index++)
            {
                if (result.Records[index].EventNumber != result.Records[index - 1].EventNumber + 1)
                {
                    throw new Exception(
                            string.Format("Invalid order of events has been detected in read index for the event stream '{0}'. "
                                          + "The event {1} at position {2} goes after the event {3} at position {4}",
                                          msg.EventStreamId,
                                          result.Records[index].EventNumber,
                                          result.Records[index].LogPosition,
                                          result.Records[index - 1].EventNumber,
                                          result.Records[index - 1].LogPosition));
                }
            }
        }

        private static void CheckEventsOrder(ClientMessage.ReadStreamEventsBackward msg, IndexReadStreamResult result)
        {
            for (var index = 1; index < result.Records.Length; index++)
            {
                if (result.Records[index].EventNumber != result.Records[index - 1].EventNumber - 1)
                {
                    throw new Exception(string.Format("Invalid order of events has been detected in read index for the event stream '{0}'. "
                                                      + "The event {1} at position {2} goes after the event {3} at position {4}",
                                                      msg.EventStreamId,
                                                      result.Records[index].EventNumber,
                                                      result.Records[index].LogPosition,
                                                      result.Records[index - 1].EventNumber,
                                                      result.Records[index - 1].LogPosition));
                }
            }
        }

        private ResolvedEvent[] ResolveLinkToEvents(EventRecord[] records, bool resolveLinks)
        {
            var resolved = new ResolvedEvent[records.Length];
            if (resolveLinks)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    resolved[i] = ResolveLinkToEvent(records[i]);
                }
            }
            else
            {
                for (int i = 0; i < records.Length; ++i)
                {
                    resolved[i] = new ResolvedEvent(records[i]);
                }
            }
            return resolved;
        }

        private ResolvedEvent ResolveLinkToEvent(EventRecord eventRecord)
        {
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                try
                {
                    string[] parts = Encoding.UTF8.GetString(eventRecord.Data).Split('@');
                    int eventNumber = int.Parse(parts[0]);
                    string streamId = parts[1];

                    var res = _readIndex.ReadEvent(streamId, eventNumber);
                    if (res.Result == ReadEventResult.Success)
                        return new ResolvedEvent(res.Record, eventRecord);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
            }
            return new ResolvedEvent(eventRecord);
        }

        private ReadAllResult ResolveReadAllResult(IndexReadAllResult res, bool resolveLinks)
        {
            var resolved = new ResolvedEvent[res.Records.Count];
            if (resolveLinks)
            {
                for (int i = 0; i < resolved.Length; ++i)
                {
                    var record = res.Records[i];
                    var resolvedPair = ResolveLinkToEvent(record.Event);
                    resolved[i] = new ResolvedEvent(resolvedPair.Event, resolvedPair.Link, record.CommitPosition);
                }
            }
            else
            {
                for (int i = 0; i < resolved.Length; ++i)
                {
                    resolved[i] = new ResolvedEvent(res.Records[i].Event, null, res.Records[i].CommitPosition);
                }
            }
            return new ReadAllResult(resolved, res.MaxCount, res.CurrentPos, res.NextPos, res.PrevPos, res.TfEofPosition);
        }
    }
}