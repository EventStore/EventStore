using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;
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
        private static readonly ResolvedEvent[] EmptyRecords = new ResolvedEvent[0];

        private readonly IPublisher _publisher;
        private readonly IReadIndex _readIndex;
        private readonly ICheckpoint _writerCheckpoint;
        private static readonly char[] _linkToSeparator = new char[]{'@'};

        public StorageReaderWorker(IPublisher publisher, IReadIndex readIndex, ICheckpoint writerCheckpoint)
        {
            Ensure.NotNull(publisher, "publisher");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(writerCheckpoint, "writerCheckpoint");

            _publisher = publisher;
            _readIndex = readIndex;
            _writerCheckpoint = writerCheckpoint;
        }

        void IHandle<ClientMessage.ReadEvent>.Handle(ClientMessage.ReadEvent msg)
        {
            msg.Envelope.ReplyWith(ReadEvent(msg));
        }

        void IHandle<ClientMessage.ReadStreamEventsForward>.Handle(ClientMessage.ReadStreamEventsForward msg)
        {
            var res = ReadStreamEventsForward(msg);
            switch (res.Result)
            {
                case ReadStreamResult.Success:
                case ReadStreamResult.NoStream:
                case ReadStreamResult.NotModified:
                    if (msg.LongPollTimeout.HasValue && res.FromEventNumber > res.LastEventNumber)
                    {
                        _publisher.Publish(new SubscriptionMessage.PollStream(
                            msg.EventStreamId, res.TfLastCommitPosition, res.LastEventNumber,
                            DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
                    }
                    else
                    {
                        msg.Envelope.ReplyWith(res);
                    }
                    break;
                case ReadStreamResult.StreamDeleted:
                case ReadStreamResult.Error:
                case ReadStreamResult.AccessDenied:
                    msg.Envelope.ReplyWith(res);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unknown ReadStreamResult: {0}", res.Result));
            }
        }

        void IHandle<ClientMessage.ReadStreamEventsBackward>.Handle(ClientMessage.ReadStreamEventsBackward msg)
        {
            if(msg.EventStreamId == SystemStreams.PersistentSubscriptionConfig) Console.Write("hello");
            msg.Envelope.ReplyWith(ReadStreamEventsBackward(msg));
        }

        void IHandle<ClientMessage.ReadAllEventsForward>.Handle(ClientMessage.ReadAllEventsForward msg)
        {
            var res = ReadAllEventsForward(msg);
            switch (res.Result)
            {
                case ReadAllResult.Success:
                    if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.Events.Length == 0)
                    {
                        _publisher.Publish(new SubscriptionMessage.PollStream(
                            SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null,
                            DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
                    }
                    else
                        msg.Envelope.ReplyWith(res);
                    break;
                case ReadAllResult.NotModified:
                    if (msg.LongPollTimeout.HasValue && res.IsEndOfStream && res.CurrentPos.CommitPosition > res.TfLastCommitPosition)
                    {
                        _publisher.Publish(new SubscriptionMessage.PollStream(
                            SubscriptionsService.AllStreamsSubscriptionId, res.TfLastCommitPosition, null, 
                            DateTime.UtcNow + msg.LongPollTimeout.Value, msg));
                    }
                    else
                        msg.Envelope.ReplyWith(res);
                break;
                case ReadAllResult.Error:
                case ReadAllResult.AccessDenied:
                    msg.Envelope.ReplyWith(res);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unknown ReadAllResult: {0}", res.Result));
            }
        }

        void IHandle<ClientMessage.ReadAllEventsBackward>.Handle(ClientMessage.ReadAllEventsBackward msg)
        {
            msg.Envelope.ReplyWith(ReadAllEventsBackward(msg));
        }

        void IHandle<StorageMessage.CheckStreamAccess>.Handle(StorageMessage.CheckStreamAccess msg)
        {
            msg.Envelope.ReplyWith(CheckStreamAccess(msg));
        }

        private ClientMessage.ReadEventCompleted ReadEvent(ClientMessage.ReadEvent msg)
        {
            try
            {
                var access = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
                if (!access.Granted)
                    return NoData(msg, ReadEventResult.AccessDenied);

                var result = _readIndex.ReadEvent(msg.EventStreamId, msg.EventNumber);
                var record = result.Result == ReadEventResult.Success && msg.ResolveLinkTos
                                     ? ResolveLinkToEvent(result.Record, msg.User)
                                     : new ResolvedEvent(result.Record);
                if (record == null)
                    return NoData(msg, ReadEventResult.AccessDenied);

                return new ClientMessage.ReadEventCompleted(msg.CorrelationId, msg.EventStreamId, result.Result,
                                                            record.Value, result.Metadata, access.Public, null);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing ReadEvent request.");
                return NoData(msg, ReadEventResult.Error, exc.Message);
            }
        }

        private ClientMessage.ReadStreamEventsForwardCompleted ReadStreamEventsForward(ClientMessage.ReadStreamEventsForward msg)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                if (msg.ValidationStreamVersion.HasValue && _readIndex.GetStreamLastEventNumber(msg.EventStreamId) == msg.ValidationStreamVersion)
                    return NoData(msg, ReadStreamResult.NotModified, lastCommitPosition, msg.ValidationStreamVersion.Value);

                var access = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
                if (!access.Granted)
                    return NoData(msg, ReadStreamResult.AccessDenied, lastCommitPosition);

                var result = _readIndex.ReadStreamEventsForward(msg.EventStreamId, msg.FromEventNumber, msg.MaxCount);
                CheckEventsOrder(msg, result);
                var resolvedPairs = ResolveLinkToEvents(result.Records, msg.ResolveLinkTos, msg.User);
                if (resolvedPairs == null)
                    return NoData(msg, ReadStreamResult.AccessDenied, lastCommitPosition);

                return new ClientMessage.ReadStreamEventsForwardCompleted(
                    msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount,
                    (ReadStreamResult) result.Result, resolvedPairs, result.Metadata, access.Public, string.Empty,
                    result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastCommitPosition);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing ReadStreamEventsForward request.");
                return NoData(msg, ReadStreamResult.Error, lastCommitPosition, error: exc.Message);
            }
        }

        private ClientMessage.ReadStreamEventsBackwardCompleted ReadStreamEventsBackward(ClientMessage.ReadStreamEventsBackward msg)
        {
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                if (msg.ValidationStreamVersion.HasValue && _readIndex.GetStreamLastEventNumber(msg.EventStreamId) == msg.ValidationStreamVersion)
                    return NoData(msg, ReadStreamResult.NotModified, lastCommitPosition, msg.ValidationStreamVersion.Value);

                var access = _readIndex.CheckStreamAccess(msg.EventStreamId, StreamAccessType.Read, msg.User);
                if (!access.Granted)
                    return NoData(msg, ReadStreamResult.AccessDenied, lastCommitPosition);

                var result = _readIndex.ReadStreamEventsBackward(msg.EventStreamId, msg.FromEventNumber, msg.MaxCount);
                CheckEventsOrder(msg, result);
                var resolvedPairs = ResolveLinkToEvents(result.Records, msg.ResolveLinkTos, msg.User);
                if (resolvedPairs == null)
                    return NoData(msg, ReadStreamResult.AccessDenied, lastCommitPosition);

                return new ClientMessage.ReadStreamEventsBackwardCompleted(
                    msg.CorrelationId, msg.EventStreamId, result.FromEventNumber, result.MaxCount,
                    (ReadStreamResult)result.Result, resolvedPairs, result.Metadata, access.Public, string.Empty,
                    result.NextEventNumber, result.LastEventNumber, result.IsEndOfStream, lastCommitPosition);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing ReadStreamEventsBackward request.");
                return NoData(msg, ReadStreamResult.Error, lastCommitPosition, error: exc.Message);
            }
        }

        private ClientMessage.ReadAllEventsForwardCompleted ReadAllEventsForward(ClientMessage.ReadAllEventsForward msg)
        {
            var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                if (pos == TFPos.HeadOfTf)
                {
                    var checkpoint = _writerCheckpoint.Read();
                    pos = new TFPos(checkpoint, checkpoint);
                }
                if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
                    return NoData(msg, ReadAllResult.Error, pos, lastCommitPosition, "Invalid position.");
                if (msg.ValidationTfLastCommitPosition == lastCommitPosition)
                    return NoData(msg, ReadAllResult.NotModified, pos, lastCommitPosition);
                var access = _readIndex.CheckStreamAccess(SystemStreams.AllStream, StreamAccessType.Read, msg.User);
                if (!access.Granted)
                    return NoData(msg, ReadAllResult.AccessDenied, pos, lastCommitPosition);


                var res = _readIndex.ReadAllEventsForward(pos, msg.MaxCount);
                var resolved = ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User);
                if (resolved == null)
                    return NoData(msg, ReadAllResult.AccessDenied, pos, lastCommitPosition);

                var metadata = _readIndex.GetStreamMetadata(SystemStreams.AllStream);
                return new ClientMessage.ReadAllEventsForwardCompleted(
                    msg.CorrelationId, ReadAllResult.Success, null, resolved, metadata, access.Public, msg.MaxCount,
                    res.CurrentPos, res.NextPos, res.PrevPos, lastCommitPosition);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing ReadAllEventsForward request.");
                return NoData(msg, ReadAllResult.Error, pos, lastCommitPosition, exc.Message);
            }
        }

        private ClientMessage.ReadAllEventsBackwardCompleted ReadAllEventsBackward(ClientMessage.ReadAllEventsBackward msg)
        {
            var pos = new TFPos(msg.CommitPosition, msg.PreparePosition);
            var lastCommitPosition = _readIndex.LastCommitPosition;
            try
            {
                if (pos == TFPos.HeadOfTf)
                {
                    var checkpoint = _writerCheckpoint.Read();
                    pos = new TFPos(checkpoint, checkpoint);
                }
                if (pos.CommitPosition < 0 || pos.PreparePosition < 0)
                    return NoData(msg, ReadAllResult.Error, pos, lastCommitPosition, "Invalid position.");
                if (msg.ValidationTfLastCommitPosition == lastCommitPosition)
                    return NoData(msg, ReadAllResult.NotModified, pos, lastCommitPosition);

                var access = _readIndex.CheckStreamAccess(SystemStreams.AllStream, StreamAccessType.Read, msg.User);
                if (!access.Granted)
                    return NoData(msg, ReadAllResult.AccessDenied, pos, lastCommitPosition);

                var res = _readIndex.ReadAllEventsBackward(pos, msg.MaxCount);
                var resolved = ResolveReadAllResult(res.Records, msg.ResolveLinkTos, msg.User);
                if (resolved == null)
                    return NoData(msg, ReadAllResult.AccessDenied, pos, lastCommitPosition);

                var metadata = _readIndex.GetStreamMetadata(SystemStreams.AllStream);
                return new ClientMessage.ReadAllEventsBackwardCompleted(
                    msg.CorrelationId, ReadAllResult.Success, null, resolved, metadata, access.Public, msg.MaxCount,
                    res.CurrentPos, res.NextPos, res.PrevPos, lastCommitPosition);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing ReadAllEventsBackward request.");
                return NoData(msg, ReadAllResult.Error, pos, lastCommitPosition, exc.Message);
            }
        }

        private StorageMessage.CheckStreamAccessCompleted CheckStreamAccess(StorageMessage.CheckStreamAccess msg)
        {
            string streamId = msg.EventStreamId;
            try
            {
                if (msg.EventStreamId == null)
                {
                    if (msg.TransactionId == null) throw new Exception("No transaction ID specified.");
                    streamId = _readIndex.GetEventStreamIdByTransactionId(msg.TransactionId.Value);
                    if (streamId == null)
                        throw new Exception(string.Format("No transaction with ID {0} found.", msg.TransactionId));
                }
                var result = _readIndex.CheckStreamAccess(streamId, msg.AccessType, msg.User);
                return new StorageMessage.CheckStreamAccessCompleted(msg.CorrelationId, streamId, msg.TransactionId, msg.AccessType, result);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error during processing CheckStreamAccess({0}, {1}) request.", msg.EventStreamId, msg.TransactionId);
                return new StorageMessage.CheckStreamAccessCompleted(msg.CorrelationId, streamId, msg.TransactionId, 
                                                                     msg.AccessType, new StreamAccess(false));
            }
        }

        private static ClientMessage.ReadEventCompleted NoData(ClientMessage.ReadEvent msg, ReadEventResult result, string error = null)
        {
            return new ClientMessage.ReadEventCompleted(msg.CorrelationId, msg.EventStreamId, result, new ResolvedEvent(null), null, false, error);
        }

        private static ClientMessage.ReadStreamEventsForwardCompleted NoData(ClientMessage.ReadStreamEventsForward msg, ReadStreamResult result, long lastCommitPosition, int lastEventNumber = -1, string error = null)
        {
            return new ClientMessage.ReadStreamEventsForwardCompleted(
                msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, result, 
                EmptyRecords, null, false, error ?? string.Empty, -1, lastEventNumber, true, lastCommitPosition);
        }

        private static ClientMessage.ReadStreamEventsBackwardCompleted NoData(ClientMessage.ReadStreamEventsBackward msg, ReadStreamResult result, long lastCommitPosition, int lastEventNumber = -1, string error = null)
        {
            return new ClientMessage.ReadStreamEventsBackwardCompleted(
                msg.CorrelationId, msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, result,
                EmptyRecords, null, false, error ?? string.Empty, -1, lastEventNumber, true, lastCommitPosition);
        }

        private ClientMessage.ReadAllEventsForwardCompleted NoData(ClientMessage.ReadAllEventsForward msg, ReadAllResult result, TFPos pos, long lastCommitPosition, string error = null)
        {
            return new ClientMessage.ReadAllEventsForwardCompleted(
                msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
                msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastCommitPosition);
        }

        private ClientMessage.ReadAllEventsBackwardCompleted NoData(ClientMessage.ReadAllEventsBackward msg, ReadAllResult result, TFPos pos, long lastCommitPosition, string error = null)
        {
            return new ClientMessage.ReadAllEventsBackwardCompleted(
                msg.CorrelationId, result, error, ResolvedEvent.EmptyArray, null, false,
                msg.MaxCount, pos, TFPos.Invalid, TFPos.Invalid, lastCommitPosition);
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

        private ResolvedEvent[] ResolveLinkToEvents(EventRecord[] records, bool resolveLinks, IPrincipal user)
        {
            var resolved = new ResolvedEvent[records.Length];
            if (resolveLinks)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    var rec = ResolveLinkToEvent(records[i], user);
                    if (rec == null)
                        return null;
                    resolved[i] = rec.Value;
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

        private ResolvedEvent? ResolveLinkToEvent(EventRecord eventRecord, IPrincipal user)
        {
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                try
                {
                    string[] parts = Helper.UTF8NoBom.GetString(eventRecord.Data).Split(_linkToSeparator, 2);
                    int eventNumber = int.Parse(parts[0]);
                    string streamId = parts[1];

                    if (!_readIndex.CheckStreamAccess(streamId, StreamAccessType.Read, user).Granted)
                        return null;

                    var res = _readIndex.ReadEvent(streamId, eventNumber);
                    if (res.Result == ReadEventResult.Success)
                        return new ResolvedEvent(res.Record, eventRecord, ReadEventResult.Success);
                    return new ResolvedEvent(null, eventRecord, res.Result);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
                // return unresolved link
                return new ResolvedEvent(null, eventRecord, ReadEventResult.Error);
            }
            return new ResolvedEvent(eventRecord);
        }

        private ResolvedEvent[] ResolveReadAllResult(IList<CommitEventRecord> records, bool resolveLinks, IPrincipal user)
        {
            var result = new ResolvedEvent[records.Count];
            if (resolveLinks)
            {
                for (int i = 0; i < result.Length; ++i)
                {
                    var record = records[i];
                    var resolvedPair = ResolveLinkToEvent(record.Event, user);
                    if (resolvedPair == null)
                        return null;
                    result[i] = new ResolvedEvent(
                        resolvedPair.Value.Event, resolvedPair.Value.Link, record.CommitPosition,
                        resolvedPair.Value.ResolveResult);
                }
            }
            else
            {
                for (int i = 0; i < result.Length; ++i)
                {
                    result[i] = new ResolvedEvent(
                        records[i].Event, null, records[i].CommitPosition, default(ReadEventResult));
                }
            }
            return result;
        }
    }
}