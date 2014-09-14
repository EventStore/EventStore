using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionParams
    {
        private bool _resolveLinkTos;
        private string _subscriptionId;
        private string _eventStreamId;
        private string _groupName;
        private bool _startFromBeginning;
        private bool _trackLatency;
        private TimeSpan _messageTimeout;
        private readonly bool _preferOne;
        private IPersistentSubscriptionEventLoader _eventLoader;
        private IPersistentSubscriptionCheckpointReader _checkpointReader;
        private IPersistentSubscriptionCheckpointWriter _checkpointWriter;

        public PersistentSubscriptionParams(bool resolveLinkTos, string subscriptionId, string eventStreamId, string groupName, bool startFromBeginning, bool trackLatency, TimeSpan messageTimeout, bool preferOne, IPersistentSubscriptionEventLoader eventLoader, IPersistentSubscriptionCheckpointReader checkpointReader, IPersistentSubscriptionCheckpointWriter checkpointWriter)
        {
            _resolveLinkTos = resolveLinkTos;
            _subscriptionId = subscriptionId;
            _eventStreamId = eventStreamId;
            _groupName = groupName;
            _startFromBeginning = startFromBeginning;
            _trackLatency = trackLatency;
            _messageTimeout = messageTimeout;
            _preferOne = preferOne;
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

        public bool StartFromBeginning
        {
            get { return _startFromBeginning; }
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

        public bool PreferOne
        {
            get { return _preferOne; }
        }
    }
}