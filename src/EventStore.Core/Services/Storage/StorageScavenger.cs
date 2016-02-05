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
    public class StorageScavenger: IHandle<ClientMessage.ScavengeDatabase>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly IODispatcher _ioDispatcher;
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly IReadIndex _readIndex;
        private readonly bool _alwaysKeepScavenged;
        private readonly bool _mergeChunks;
        private readonly string _nodeEndpoint;
        private readonly int _scavengeHistoryMaxAge;

        private int _isScavengingRunning;

        public StorageScavenger(TFChunkDb db, IODispatcher ioDispatcher, ITableIndex tableIndex, IHasher hasher,
                                IReadIndex readIndex, bool alwaysKeepScavenged, string nodeEndpoint, bool mergeChunks, int scavengeHistoryMaxAge)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(ioDispatcher, "ioDispatcher");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            Ensure.NotNull(readIndex, "readIndex");
            Ensure.NotNull(nodeEndpoint, "nodeEndpoint");

            _db = db;
            _ioDispatcher = ioDispatcher;
            _tableIndex = tableIndex;
            _hasher = hasher;
            _readIndex = readIndex;
            _alwaysKeepScavenged = alwaysKeepScavenged;
            _mergeChunks = mergeChunks;
            _nodeEndpoint = nodeEndpoint;
            _scavengeHistoryMaxAge = scavengeHistoryMaxAge;
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
                    
                    var scavenger = new TFChunkScavenger(_db, _ioDispatcher, _tableIndex, _hasher, _readIndex, scavengeId, _nodeEndpoint);
					spaceSaved = scavenger.Scavenge(_alwaysKeepScavenged, _mergeChunks);
                    result = ClientMessage.ScavengeDatabase.ScavengeResult.Success;
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

        private void WriteScavengeStartedEvent(string streamName, Guid scavengeId)
        {
            var metadataEventId = Guid.NewGuid();
            var metaStreamId = SystemStreams.MetastreamOf(streamName);
            var metadata = new StreamMetadata(maxAge: TimeSpan.FromDays(_scavengeHistoryMaxAge));
            var metaStreamEvent = new Event(metadataEventId, SystemEventTypes.StreamMetadata, isJson: true,
                                            data: metadata.ToJsonBytes(), metadata: null);
            _ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent, SystemAccount.Principal, m => { });

            var scavengeStartedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeStarted, true, new Dictionary<string, object>{
                    {"scavengeId", scavengeId},
                    {"nodeEndpoint", _nodeEndpoint},
                }.ToJsonBytes(), null);
            _ioDispatcher.WriteEvent(streamName, ExpectedVersion.Any, scavengeStartedEvent, SystemAccount.Principal, x => WriteScavengeEventCompleted(x, streamName));
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
            _ioDispatcher.WriteEvent(streamName, ExpectedVersion.Any, scavengeCompletedEvent, SystemAccount.Principal, x => WriteScavengeEventCompleted(x, streamName));
        }

        private void WriteScavengeEventCompleted(ClientMessage.WriteEventsCompleted msg, string streamId)
        {
            string eventLinkTo = string.Format("{0}@{1}", msg.FirstEventNumber, streamId);
            var linkToIndexEvent = new Event(Guid.NewGuid(), SystemEventTypes.LinkTo, false, eventLinkTo, null);
            _ioDispatcher.WriteEvent(SystemStreams.ScavengesStream, ExpectedVersion.Any, linkToIndexEvent, SystemAccount.Principal, n => { });
        }
    }
}
