using System;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkScavengerLogManager : ITFChunkScavengerLogManager
    {
        private readonly string _nodeEndpoint;
        private readonly TimeSpan _scavengeHistoryMaxAge;
        private readonly IODispatcher _ioDispatcher;
        private const int MaxRetryCount = 5;
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        public TFChunkScavengerLogManager(string nodeEndpoint, TimeSpan scavengeHistoryMaxAge, IODispatcher ioDispatcher)
        {
            _nodeEndpoint = nodeEndpoint;
            _scavengeHistoryMaxAge = scavengeHistoryMaxAge;
            _ioDispatcher = ioDispatcher;
        }

        public void Initialise()
        {
            var metaStreamId = SystemStreams.MetastreamOf(SystemStreams.ScavengesStream);
            var metadata = new StreamMetadata(maxAge: _scavengeHistoryMaxAge);
            var metaStreamEvent = new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, isJson: true,
                data: metadata.ToJsonBytes(), metadata: null);
            _ioDispatcher.WriteEvent(metaStreamId, ExpectedVersion.Any, metaStreamEvent, SystemAccount.Principal, m => {
                if (m.Result != OperationResult.Success)
                {
                    Log.Error("Failed to write the $maxAge of {0} days metadata for the {1} stream. Reason: {2}", _scavengeHistoryMaxAge.TotalDays, SystemStreams.ScavengesStream, m.Result);
                }
            });
        }

        public ITFChunkScavengerLog CreateLog()
        {
            return new TFChunkScavengerLog(_ioDispatcher, Guid.NewGuid(), _nodeEndpoint, MaxRetryCount, _scavengeHistoryMaxAge);
        }
    }
}