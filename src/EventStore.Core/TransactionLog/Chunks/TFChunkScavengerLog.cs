using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.TransactionLog.Chunks
{
    class TFChunkScavengerLog : ITFChunkScavengerLog
    {
        private readonly string _streamName;
        private readonly IODispatcher _ioDispatcher;
        private readonly string _scavengeId;
        private readonly string _nodeId;
        private readonly int _retryAttempts;
        private readonly TimeSpan _scavengeHistoryMaxAge;
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();
        private long _spaceSaved;

        public TFChunkScavengerLog(IODispatcher ioDispatcher, string scavengeId, string nodeId, int retryAttempts, TimeSpan scavengeHistoryMaxAge)
        {
            _ioDispatcher = ioDispatcher;
            _scavengeId = scavengeId;
            _nodeId = nodeId;
            _retryAttempts = retryAttempts;
            _scavengeHistoryMaxAge = scavengeHistoryMaxAge;

            _streamName = string.Format("{0}-{1}", SystemStreams.ScavengesStream, scavengeId);
        }

        public string ScavengeId => _scavengeId;

        public void ScavengeStarted()
        {
            var metadataEventId = Guid.NewGuid();
            var metaStreamId = SystemStreams.MetastreamOf(_streamName);
            var metadata = new StreamMetadata(maxAge: _scavengeHistoryMaxAge);
            var metaStreamEvent = new Event(metadataEventId, SystemEventTypes.StreamMetadata, isJson: true,
                data: metadata.ToJsonBytes(), metadata: null);
            _ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent, SystemAccount.Principal, m => {
                if (m.Result != OperationResult.Success)
                {
                    Log.Error("Failed to write the $maxAge of {0} days metadata for the {1} stream. Reason: {2}", _scavengeHistoryMaxAge.TotalDays, _streamName, m.Result);
                }
            });

            var scavengeStartedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeStarted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"nodeEndpoint", _nodeId},
            }.ToJsonBytes(), null);
            WriteScavengeDetailEvent(_streamName, scavengeStartedEvent, _retryAttempts);
        }

        public void ScavengeCompleted(ScavengeResult result, string error, TimeSpan elapsed)
        {
            var scavengeCompletedEvent = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"nodeEndpoint", _nodeId},
                {"result", result},
                {"error", error},
                {"timeTaken", elapsed},
                {"spaceSaved", _spaceSaved}
            }.ToJsonBytes(), null);
            WriteScavengeDetailEvent(_streamName, scavengeCompletedEvent, _retryAttempts);
        }

        public void ChunksScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved)
        {
            Interlocked.Add(ref _spaceSaved, spaceSaved);
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeChunksCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"chunkStartNumber", chunkStartNumber},
                {"chunkEndNumber", chunkEndNumber},
                {"timeTaken", elapsed},
                {"wasScavenged", true},
                {"spaceSaved", spaceSaved},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", ""}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }

        public void ChunksNotScavenged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage)
        {
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeChunksCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"chunkStartNumber", chunkStartNumber},
                {"chunkEndNumber", chunkEndNumber},
                {"timeTaken", elapsed},
                {"wasScavenged", false},
                {"spaceSaved", 0},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", errorMessage}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }

        public void ChunksMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, long spaceSaved)
        {
            Interlocked.Add(ref _spaceSaved, spaceSaved);
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeMergeCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"chunkStartNumber", chunkStartNumber},
                {"chunkEndNumber", chunkEndNumber},
                {"timeTaken", elapsed},
                {"spaceSaved", spaceSaved},
                {"wasMerged", true},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", ""}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }

        public void ChunksNotMerged(int chunkStartNumber, int chunkEndNumber, TimeSpan elapsed, string errorMessage)
        {
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeMergeCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"chunkStartNumber", chunkStartNumber},
                {"chunkEndNumber", chunkEndNumber},
                {"timeTaken", elapsed},
                {"spaceSaved", 0},
                {"wasMerged", false},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", errorMessage}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }


        public void IndexTableScavenged(int level, int index, TimeSpan elapsed, long entriesDeleted, long entriesKept,
            long spaceSaved)
        {
            Interlocked.Add(ref _spaceSaved, spaceSaved);
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeIndexCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"level", level},
                {"index", index},
                {"timeTaken", elapsed},
                {"entriesDeleted", entriesDeleted},
                {"entriesKept", entriesKept},
                {"spaceSaved", spaceSaved},
                {"wasScavenged", true},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", ""}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }

        public void IndexTableNotScavenged(int level, int index, TimeSpan elapsed, long entriesKept, string errorMessage)
        {
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeIndexCompleted, true, new Dictionary<string, object>{
                {"scavengeId", _scavengeId},
                {"level", level},
                {"index", index},
                {"timeTaken", elapsed},
                {"entriesDeleted", 0},
                {"entriesKept", entriesKept},
                {"spaceSaved", 0},
                {"wasScavenged", false},
                {"nodeEndpoint", _nodeId},
                {"errorMessage", errorMessage}
            }.ToJsonBytes(), null);

            WriteScavengeChunkCompletedEvent(_streamName, evnt, _retryAttempts);
        }

        private void WriteScavengeChunkCompletedEvent(string streamId, Event eventToWrite, int retryCount)
        {
            _ioDispatcher.WriteEvent(streamId, ExpectedVersion.Any, eventToWrite, SystemAccount.Principal, m => WriteScavengeChunkCompletedEventCompleted(m, streamId, eventToWrite, retryCount));
        }

        private void WriteScavengeChunkCompletedEventCompleted(ClientMessage.WriteEventsCompleted msg, string streamId, Event eventToWrite, int retryCount)
        {
            if (msg.Result != OperationResult.Success)
            {
                if (retryCount > 0)
                {
                    WriteScavengeChunkCompletedEvent(streamId, eventToWrite, --retryCount);
                }
                else
                {
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", streamId, _retryAttempts, msg.Result);
                }
            }
        }

        private void WriteScavengeDetailEvent(string streamId, Event eventToWrite, int retryCount)
        {
            _ioDispatcher.WriteEvent(streamId, ExpectedVersion.Any, eventToWrite, SystemAccount.Principal, x => WriteScavengeDetailEventCompleted(x, eventToWrite, streamId, retryCount));
        }

        private void WriteScavengeIndexEvent(Event linkToEvent, int retryCount)
        {
            _ioDispatcher.WriteEvent(SystemStreams.ScavengesStream, ExpectedVersion.Any, linkToEvent, SystemAccount.Principal, m => WriteScavengeIndexEventCompleted(m, linkToEvent, retryCount));
        }

        private void WriteScavengeIndexEventCompleted(ClientMessage.WriteEventsCompleted msg, Event linkToEvent, int retryCount)
        {
            if (msg.Result != OperationResult.Success)
            {
                if (retryCount > 0)
                {
                    Log.Error("Failed to write an event to the {0} stream. Retrying {1}/{2}. Reason: {3}", SystemStreams.ScavengesStream, (_retryAttempts - retryCount) + 1, _retryAttempts, msg.Result);
                    WriteScavengeIndexEvent(linkToEvent, --retryCount);
                }
                else
                {
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", SystemStreams.ScavengesStream, _retryAttempts, msg.Result);
                }
            }
        }

        private void WriteScavengeDetailEventCompleted(ClientMessage.WriteEventsCompleted msg, Event eventToWrite, string streamId, int retryCount)
        {
            if (msg.Result != OperationResult.Success)
            {
                if (retryCount > 0)
                {
                    Log.Error("Failed to write an event to the {0} stream. Retrying {1}/{2}. Reason: {3}", streamId, (_retryAttempts - retryCount) + 1, _retryAttempts, msg.Result);
                    WriteScavengeDetailEvent(streamId, eventToWrite, --retryCount);
                }
                else
                {
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", streamId, _retryAttempts, msg.Result);
                }
            }
            else
            {
                string eventLinkTo = string.Format("{0}@{1}", msg.FirstEventNumber, streamId);
                var linkToIndexEvent = new Event(Guid.NewGuid(), SystemEventTypes.LinkTo, false, eventLinkTo, null);
                WriteScavengeIndexEvent(linkToIndexEvent, _retryAttempts);
            }
        }

    }
}