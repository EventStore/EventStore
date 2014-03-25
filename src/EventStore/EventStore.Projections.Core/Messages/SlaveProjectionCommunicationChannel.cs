using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages
{
    public sealed class SlaveProjectionCommunicationChannel
    {
        private readonly Guid _coreProjectionId;
        private readonly Guid _subscriptionId;
        private readonly IPublisher _publishEnvelope;
        private readonly string _managedProjectionName;

        public SlaveProjectionCommunicationChannel(
            string managedProjectionName, Guid coreProjectionId, Guid subscriptionId, IPublisher publishEnvelope)
        {
            _managedProjectionName = managedProjectionName;
            _coreProjectionId = coreProjectionId;
            _subscriptionId = subscriptionId;
            _publishEnvelope = publishEnvelope;
        }

        public Guid CoreProjectionId
        {
            get { return _coreProjectionId; }
        }

        public IPublisher PublishEnvelope
        {
            get { return _publishEnvelope; }
        }

        public Guid SubscriptionId
        {
            get { return _subscriptionId; }
        }

        public string ManagedProjectionName
        {
            get { return _managedProjectionName; }
        }
    }
}
