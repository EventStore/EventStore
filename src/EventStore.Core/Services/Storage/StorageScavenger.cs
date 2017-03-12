using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage
{
    public class StorageScavenger : IHandle<ClientMessage.ScavengeDatabase>,
                                    IHandle<UserManagementMessage.UserManagementServiceInitialized>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly IODispatcher _ioDispatcher;
        private readonly ITableIndex _tableIndex;
        private readonly IReadIndex _readIndex;
        private readonly bool _alwaysKeepScavenged;
        private readonly bool _mergeChunks;
        private readonly string _nodeEndpoint;
        private readonly int _scavengeHistoryMaxAge;
        private readonly bool _unsafeIgnoreHardDeletes;
        private int _isScavengingRunning;
        private const int MaxRetryCount = 5;

        public StorageScavenger(TFChunkDb db, IODispatcher ioDispatcher, ITableIndex tableIndex,
                                IReadIndex readIndex, bool alwaysKeepScavenged, string nodeEndpoint, bool mergeChunks,
                                int scavengeHistoryMaxAge, bool unsafeIgnoreHardDeletes)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(ioDispatcher, "ioDispatcher");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(nodeEndpoint, "nodeEndpoint");

            _db = db;
            _ioDispatcher = ioDispatcher;
            _tableIndex = tableIndex;
            _readIndex = readIndex;
            _alwaysKeepScavenged = alwaysKeepScavenged;
            _mergeChunks = mergeChunks;
            _nodeEndpoint = nodeEndpoint;
            _scavengeHistoryMaxAge = scavengeHistoryMaxAge;
            _unsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
        }

        public void Handle(UserManagementMessage.UserManagementServiceInitialized message)
        {
            WriteScavengeIndexInitializedEvent();
        }

        public void Handle(ClientMessage.ScavengeDatabase message)
        {
            if (Interlocked.CompareExchange(ref _isScavengingRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Scavenge(message));
            }
            else
            {
                message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseCompleted(message.CorrelationId,
                                                            ClientMessage.ScavengeDatabase.ScavengeResult.InProgress,
                                                            "Scavenge already in progress.",
                                                            TimeSpan.FromMilliseconds(0),
                                                            0));
            }
        }

        private void Scavenge(ClientMessage.ScavengeDatabase message)
        {
            var sw = Stopwatch.StartNew();
            Guid scavengeId = Guid.NewGuid();
            var streamName = string.Format("{0}-{1}", SystemStreams.ScavengesStream, scavengeId);
            ClientMessage.ScavengeDatabase.ScavengeResult result;
            string error = null;
            long spaceSaved = 0;
            try
            {
                if (message.User == null || (!message.User.IsInRole(SystemRoles.Admins) && !message.User.IsInRole(SystemRoles.Operations)))
                {
                    result = ClientMessage.ScavengeDatabase.ScavengeResult.Failed;
                    error = "Access denied.";
                }
                else
                {
                    WriteScavengeStartedEvent(streamName, scavengeId);

                    var scavenger = new TFChunkScavenger(_db, _ioDispatcher, _tableIndex, _readIndex, scavengeId,
                                                                                 _nodeEndpoint, unsafeIgnoreHardDeletes: _unsafeIgnoreHardDeletes);
                    int failedCount;
                    spaceSaved = scavenger.Scavenge(_alwaysKeepScavenged, _mergeChunks, out failedCount);
                    result = failedCount > 0
                        ? ClientMessage.ScavengeDatabase.ScavengeResult.Warning
                        : ClientMessage.ScavengeDatabase.ScavengeResult.Success;
                }
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
                result = ClientMessage.ScavengeDatabase.ScavengeResult.Failed;
                error = string.Format("Error while scavenging DB: {0}.", exc.Message);
            }

            Interlocked.Exchange(ref _isScavengingRunning, 0);

            WriteScavengeCompletedEvent(streamName, scavengeId, result, error, spaceSaved, sw.Elapsed);
            message.Envelope.ReplyWith(
                new ClientMessage.ScavengeDatabaseCompleted(message.CorrelationId, result, error, sw.Elapsed, spaceSaved)
            );
        }

        private void WriteScavengeIndexInitializedEvent()
        {
            var metadataEventId = Guid.NewGuid();
            var metaStreamId = SystemStreams.MetastreamOf(SystemStreams.ScavengesStream);
            var metadata = new StreamMetadata(maxAge: TimeSpan.FromDays(_scavengeHistoryMaxAge));
            var metaStreamEvent = new Event(metadataEventId, SystemEventTypes.StreamMetadata, isJson: true,
                                            data: metadata.ToJsonBytes(), metadata: null);
            _ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent, SystemAccount.Principal, m => { 
                if(m.Result != OperationResult.Success){
                    Log.Error("Failed to write the $maxAge of {0} days metadata for the {1} stream. Reason: {2}", _scavengeHistoryMaxAge, SystemStreams.ScavengesStream, m.Result);
                }
            });

            var indexInitializedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeIndexInitialized,
                    true, new Dictionary<string, object>{}.ToJsonBytes(), null);
            _ioDispatcher.WriteEvent(SystemStreams.ScavengesStream, ExpectedVersion.NoStream, indexInitializedEvent, SystemAccount.Principal, m => {
                if(m.Result != OperationResult.Success && m.Result != OperationResult.WrongExpectedVersion){
                    Log.Error("Failed to write the {0} event to the {1} stream. Reason: {2}", SystemEventTypes.ScavengeIndexInitialized, SystemStreams.ScavengesStream, m.Result);
                }
             });
        }

        private void WriteScavengeStartedEvent(string streamName, Guid scavengeId)
        {
            var metadataEventId = Guid.NewGuid();
            var metaStreamId = SystemStreams.MetastreamOf(streamName);
            var metadata = new StreamMetadata(maxAge: TimeSpan.FromDays(_scavengeHistoryMaxAge));
            var metaStreamEvent = new Event(metadataEventId, SystemEventTypes.StreamMetadata, isJson: true,
                                            data: metadata.ToJsonBytes(), metadata: null);
            _ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent, SystemAccount.Principal, m => { 
                if(m.Result != OperationResult.Success){
                    Log.Error("Failed to write the $maxAge of {0} days metadata for the {1} stream. Reason: {2}", _scavengeHistoryMaxAge, streamName, m.Result);
                }
            });

            var scavengeStartedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeStarted, true, new Dictionary<string, object>{
                    {"scavengeId", scavengeId},
                    {"nodeEndpoint", _nodeEndpoint},
                }.ToJsonBytes(), null);
            WriteScavengeDetailEvent(streamName, scavengeStartedEvent, MaxRetryCount);
        }

        private void WriteScavengeCompletedEvent(string streamName, Guid scavengeId, ClientMessage.ScavengeDatabase.ScavengeResult result, string error, long spaceSaved, TimeSpan timeTaken)
        {
            var scavengeCompletedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeCompleted, true, new Dictionary<string, object>{
                    {"scavengeId", scavengeId},
                    {"nodeEndpoint", _nodeEndpoint},
                    {"result", result},
                    {"error", error},
                    {"timeTaken", timeTaken},
                    {"spaceSaved", spaceSaved}
                }.ToJsonBytes(), null);
            WriteScavengeDetailEvent(streamName, scavengeCompletedEvent, MaxRetryCount);
        }

        private void WriteScavengeDetailEvent(string streamId, Event eventToWrite, int retryCount){
            _ioDispatcher.WriteEvent(streamId, ExpectedVersion.Any, eventToWrite, SystemAccount.Principal, x => WriteScavengeDetailEventCompleted(x, eventToWrite, streamId, retryCount));
        }

        private void WriteScavengeIndexEvent(Event linkToEvent, int retryCount){
            _ioDispatcher.WriteEvent(SystemStreams.ScavengesStream, ExpectedVersion.Any, linkToEvent, SystemAccount.Principal, m => WriteScavengeIndexEventCompleted(m, linkToEvent, retryCount));
        }

        private void WriteScavengeIndexEventCompleted(ClientMessage.WriteEventsCompleted msg, Event linkToEvent, int retryCount){
            if(msg.Result != OperationResult.Success){
                if(retryCount > 0){
                    Log.Error("Failed to write an event to the {0} stream. Retrying {1}/{2}. Reason: {3}", SystemStreams.ScavengesStream, (MaxRetryCount - retryCount) + 1, MaxRetryCount, msg.Result);
                    WriteScavengeIndexEvent(linkToEvent, --retryCount);
                }else{
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", SystemStreams.ScavengesStream, MaxRetryCount, msg.Result);
                }
            }
        }

        private void WriteScavengeDetailEventCompleted(ClientMessage.WriteEventsCompleted msg, Event eventToWrite, string streamId, int retryCount)
        {
            if(msg.Result != OperationResult.Success){
                if(retryCount > 0){
                    Log.Error("Failed to write an event to the {0} stream. Retrying {1}/{2}. Reason: {3}", streamId, (MaxRetryCount - retryCount) + 1, MaxRetryCount, msg.Result);
                    WriteScavengeDetailEvent(streamId, eventToWrite, --retryCount);
                }else{
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", streamId, MaxRetryCount, msg.Result);
                }
            }else{
                string eventLinkTo = string.Format("{0}@{1}", msg.FirstEventNumber, streamId);
                var linkToIndexEvent = new Event(Guid.NewGuid(), SystemEventTypes.LinkTo, false, eventLinkTo, null);
                WriteScavengeIndexEvent(linkToIndexEvent, MaxRetryCount);
            }
        }
    }
}
