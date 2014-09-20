using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionParams
    {
        private bool _resolveLinkTos;
        private string _subscriptionId;
        private string _eventStreamId;
        private string _groupName;
        private int _startFrom;
        private bool _trackLatency;
        private TimeSpan _messageTimeout;
        private readonly bool _preferRoundRobin;
        private int _maxRetryCount;
        private int _liveBufferSize;
        private int _historyBufferSize;
        private int _readBatchSize;
        private IPersistentSubscriptionEventLoader _eventLoader;
        private IPersistentSubscriptionCheckpointReader _checkpointReader;
        private IPersistentSubscriptionCheckpointWriter _checkpointWriter;

        public PersistentSubscriptionParams(bool resolveLinkTos, string subscriptionId, string eventStreamId, string groupName, 
                                           int startFrom, bool trackLatency, TimeSpan messageTimeout, bool preferRoundRobin, 
                                           int maxRetryCount, int liveBufferSize, int historyBufferSize, int readBatchSize,
                                           IPersistentSubscriptionEventLoader eventLoader, 
                                           IPersistentSubscriptionCheckpointReader checkpointReader, 
                                           IPersistentSubscriptionCheckpointWriter checkpointWriter)
        {
            _resolveLinkTos = resolveLinkTos;
            _subscriptionId = subscriptionId;
            _eventStreamId = eventStreamId;
            _groupName = groupName;
            _startFrom = startFrom;
            _trackLatency = trackLatency;
            _messageTimeout = messageTimeout;
            _preferRoundRobin = preferRoundRobin;
            MaxRetryCount = maxRetryCount;
            LiveBufferSize = liveBufferSize;
            HistoryBufferSize = historyBufferSize;
            ReadBatchSize = readBatchSize;
            _eventLoader = eventLoader;
            _checkpointReader = checkpointReader;
            _checkpointWriter = checkpointWriter;
        }

        public bool ResolveLinkTos
        {
            get { return _resolveLinkTos; }
        }

        public string SubscriptionId
        {
            get { return _subscriptionId; }
        }

        public string EventStreamId
        {
            get { return _eventStreamId; }
        }

        public string GroupName
        {
            get { return _groupName; }
        }

        public int StartFrom
        {
            get { return _startFrom; }
        }

        public bool TrackLatency
        {
            get { return _trackLatency; }
        }

        public TimeSpan MessageTimeout
        {
            get { return _messageTimeout; }
        }

        public IPersistentSubscriptionEventLoader EventLoader
        {
            get { return _eventLoader; }
        }

        public IPersistentSubscriptionCheckpointReader CheckpointReader
        {
            get { return _checkpointReader; }
        }

        public IPersistentSubscriptionCheckpointWriter CheckpointWriter
        {
            get { return _checkpointWriter; }
        }

        public bool PreferRoundRobin
        {
            get { return _preferRoundRobin; }
        }

        public int MaxRetryCount
        {
            get { return _maxRetryCount; }
            set { _maxRetryCount = value; }
        }

        public int LiveBufferSize
        {
            get { return _liveBufferSize; }
            set { _liveBufferSize = value; }
        }

        public int HistoryBufferSize
        {
            get { return _historyBufferSize; }
            set { _historyBufferSize = value; }
        }

        public int ReadBatchSize
        {
            get { return _readBatchSize; }
            set { _readBatchSize = value; }
        }
    }
}