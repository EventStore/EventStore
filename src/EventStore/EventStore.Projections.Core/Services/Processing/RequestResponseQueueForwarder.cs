using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messaging;

namespace EventStore.Projections.Core.Services.Processing
{
    public class RequestResponseQueueForwarder : IHandle<ClientMessage.ReadEvent>,
                                                 IHandle<ClientMessage.ReadStreamEventsBackward>,
                                                 IHandle<ClientMessage.ReadStreamEventsForward>,
                                                 IHandle<ClientMessage.ReadAllEventsForward>,
                                                 IHandle<ClientMessage.WriteEvents>
    {
        private readonly IPublisher _externalRequestQueue;
        private readonly IPublisher _inputQueue;

        public RequestResponseQueueForwarder(IPublisher inputQueue, IPublisher externalRequestQueue)
        {
            _inputQueue = inputQueue;
            _externalRequestQueue = externalRequestQueue;
        }

        public void Handle(ClientMessage.ReadEvent msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadEvent(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.EventNumber, msg.ResolveLinkTos, msg.RequireMaster, msg.User));
        }

        public void Handle(ClientMessage.WriteEvents msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.WriteEvents(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope), false, 
                    msg.EventStreamId, msg.ExpectedVersion, msg.Events, msg.User));
        }

        public void Handle(ClientMessage.ReadStreamEventsBackward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationStreamVersion, msg.User));
        }

        public void Handle(ClientMessage.ReadStreamEventsForward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadStreamEventsForward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.EventStreamId, msg.FromEventNumber, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationStreamVersion, msg.User));
        }

        public void Handle(ClientMessage.ReadAllEventsForward msg)
        {
            _externalRequestQueue.Publish(
                new ClientMessage.ReadAllEventsForward(
                    msg.InternalCorrId, msg.CorrelationId, new PublishToWrapEnvelop(_inputQueue, msg.Envelope),
                    msg.CommitPosition, msg.PreparePosition, msg.MaxCount, msg.ResolveLinkTos, msg.RequireMaster,
                    msg.ValidationTfLastCommitPosition, msg.User));
        }
    }
}
