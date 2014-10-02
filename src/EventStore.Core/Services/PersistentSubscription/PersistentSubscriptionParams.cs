using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionParams
    {
        private readonly bool _resolveLinkTos;
        private readonly string _subscriptionId;
        private readonly string _eventStreamId;
        private readonly string _groupName;
        private readonly int _startFrom;
        private readonly bool _extraStatistics;
        private readonly TimeSpan _messageTimeout;
        private readonly TimeSpan _checkPointAfter;
        private readonly int _minCheckPointCount;
        private readonly int _maxCheckPointCount;
        private readonly bool _preferRoundRobin;
        private readonly int _maxRetryCount;
        private readonly int _liveBufferSize;
        private readonly int _bufferSize;
        private readonly int _readBatchSize;
        private readonly IPersistentSubscriptionEventLoader _eventLoader;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private IPersistentSubscriptionMessageParker _messageParker;

        public PersistentSubscriptionParams(bool resolveLinkTos, string subscriptionId, string eventStreamId, string groupName, 
                                           int startFrom, bool extraStatistics, TimeSpan messageTimeout, bool preferRoundRobin, 
                                           int maxRetryCount, int liveBufferSize, int bufferSize, int readBatchSize,
                                           TimeSpan checkPointAfter, int minCheckPointCount, int maxCheckPointCount,
                                           IPersistentSubscriptionEventLoader eventLoader, 
                                           IPersistentSubscriptionCheckpointReader checkpointReader, 
                                           IPersistentSubscriptionCheckpointWriter checkpointWriter,
                                           IPersistentSubscriptionMessageParker messageParker)
        {
            _resolveLinkTos = resolveLinkTos;
            _subscriptionId = subscriptionId;
            _eventStreamId = eventStreamId;
            _groupName = groupName;
            _startFrom = startFrom;
            _extraStatistics = extraStatistics;
            _messageTimeout = messageTimeout;
            _preferRoundRobin = preferRoundRobin;
            _maxRetryCount = maxRetryCount;
            _liveBufferSize = liveBufferSize;
            _bufferSize = bufferSize;
            _checkPointAfter = checkPointAfter;
            _minCheckPointCount = minCheckPointCount;
            _maxCheckPointCount = maxCheckPointCount;
            _readBatchSize = readBatchSize;
            _eventLoader = eventLoader;
            _checkpointReader = checkpointReader;
            _checkpointWriter = checkpointWriter;
            _messageParker = messageParker;
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

        public bool ExtraStatistics
        {
            get { return _extraStatistics; }
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

        public IPersistentSubscriptionMessageParker MessageParker
        {
            get { return _messageParker; }
        }

        public bool PreferRoundRobin
        {
            get { return _preferRoundRobin; }
        }

        public int MaxRetryCount
        {
            get { return _maxRetryCount; }
        }

        public int LiveBufferSize
        {
            get { return _liveBufferSize; }
        }

        public int BufferSize
        {
            get { return _bufferSize; }
        }

        public int ReadBatchSize
        {
            get { return _readBatchSize; }
        }

        public TimeSpan CheckPointAfter
        {
            get { return _checkPointAfter; }
        }

        public int MinCheckPointCount
        {
            get { return _minCheckPointCount; }
        }

        public int MaxCheckPointCount
        {
            get { return _maxCheckPointCount; }
        }
    }
}