using System;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionParams
    {
        private readonly bool _resolveLinkTos;
        private readonly string _subscriptionId;
        private readonly string _eventStreamId;
        private readonly string _groupName;
        private readonly long _startFrom;
        private readonly bool _extraStatistics;
        private readonly TimeSpan _messageTimeout;
        private readonly TimeSpan _checkPointAfter;
        private readonly int _minCheckPointCount;
        private readonly int _maxCheckPointCount;
        private readonly int _maxSubscriberCount;
        private readonly IPersistentSubscriptionConsumerStrategy _consumerStrategy;

        private readonly int _maxRetryCount;
        private readonly int _liveBufferSize;
        private readonly int _bufferSize;
        private readonly int _readBatchSize;
        private readonly IPersistentSubscriptionStreamReader _streamReader;
        private readonly IPersistentSubscriptionCheckpointReader _checkpointReader;
        private readonly IPersistentSubscriptionCheckpointWriter _checkpointWriter;
        private IPersistentSubscriptionMessageParker _messageParker;

        public PersistentSubscriptionParams(bool resolveLinkTos, string subscriptionId, string eventStreamId, string groupName, 
                                           long startFrom, bool extraStatistics, TimeSpan messageTimeout, 
                                           int maxRetryCount, int liveBufferSize, int bufferSize, int readBatchSize,
                                           TimeSpan checkPointAfter, int minCheckPointCount,
                                           int maxCheckPointCount, int maxSubscriberCount, 
                                           IPersistentSubscriptionConsumerStrategy consumerStrategy,
                                           IPersistentSubscriptionStreamReader streamReader, 
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
            _maxRetryCount = maxRetryCount;
            _liveBufferSize = liveBufferSize;
            _bufferSize = bufferSize;
            _checkPointAfter = checkPointAfter;
            _minCheckPointCount = minCheckPointCount;
            _maxCheckPointCount = maxCheckPointCount;
            _maxSubscriberCount = maxSubscriberCount;
            _consumerStrategy = consumerStrategy;
            _readBatchSize = readBatchSize;
            _streamReader = streamReader;
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

        public long StartFrom
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

        public IPersistentSubscriptionStreamReader StreamReader
        {
            get { return _streamReader; }
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

        public int MaxSubscriberCount
        {
            get { return _maxSubscriberCount; }
        }

        public IPersistentSubscriptionConsumerStrategy ConsumerStrategy
        {
            get { return _consumerStrategy; }
        }

        public string ParkedMessageStream
        {
            get { return "$persistentsubscription-" + _eventStreamId + "::" + _groupName + "-parked"; }
        }
    }
}