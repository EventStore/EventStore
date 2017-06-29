using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Helpers
{
    public sealed class IODispatcher : IHandle<IODispatcherDelayedMessage>
    {
        private readonly Guid _selfId = Guid.NewGuid();
        private readonly IPublisher _publisher;
        private readonly IEnvelope _inputQueueEnvelope;

        public readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted> ForwardReader;

        public readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> BackwardReader;

        public readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> Writer;

        public readonly RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>
            StreamDeleter;

        public readonly RequestResponseDispatcher<AwakeServiceMessage.SubscribeAwake, IODispatcherDelayedMessage> Awaker;

        public IODispatcher(IPublisher publisher, IEnvelope envelope)
        {
            _publisher = publisher;
            _inputQueueEnvelope = envelope;
            ForwardReader =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsForward, ClientMessage.ReadStreamEventsForwardCompleted>(
                    publisher,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    envelope);
            BackwardReader =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    publisher,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    envelope);
            Writer =
                new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                    publisher,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    envelope);

            StreamDeleter =
                new RequestResponseDispatcher<ClientMessage.DeleteStream, ClientMessage.DeleteStreamCompleted>(
                    publisher,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    envelope);

            Awaker =
                new RequestResponseDispatcher<AwakeServiceMessage.SubscribeAwake, IODispatcherDelayedMessage>(
                    publisher,
                    v => v.CorrelationId,
                    v => v.CorrelationId,
                    envelope,
                    cancelMessageFactory: requestId => new AwakeServiceMessage.UnsubscribeAwake(requestId));
        }

        public Guid ReadBackward(
            string streamId,
            long fromEventNumber,
            int maxCount,
            bool resolveLinks,
            IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted> action)
        {
            var corrId = Guid.NewGuid();
            return
                BackwardReader.Publish(
                    new ClientMessage.ReadStreamEventsBackward(
                        corrId,
                        corrId,
                        BackwardReader.Envelope,
                        streamId,
                        fromEventNumber,
                        maxCount,
                        resolveLinks,
                        false,
                        null,
                        principal),
                    action);
        }

        public Guid ReadForward(
            string streamId,
            long fromEventNumber,
            int maxCount,
            bool resolveLinks,
            IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsForwardCompleted> action,
            Guid? corrId = null)
        {
            if (!corrId.HasValue)
                corrId = Guid.NewGuid();
            return
                ForwardReader.Publish(
                    new ClientMessage.ReadStreamEventsForward(
                        corrId.Value,
                        corrId.Value,
                        ForwardReader.Envelope,
                        streamId,
                        fromEventNumber,
                        maxCount,
                        resolveLinks,
                        false,
                        null,
                        principal),
                    action);
        }

        public void ConfigureStreamAndWriteEvents(
            string streamId,
            long expectedVersion,
            Lazy<StreamMetadata> streamMetadata,
            Event[] events,
            IPrincipal principal,
            Action<ClientMessage.WriteEventsCompleted> action)
        {
            if (expectedVersion != ExpectedVersion.Any && expectedVersion != ExpectedVersion.NoStream)
                WriteEvents(streamId, expectedVersion, events, principal, action);
            else
                ReadBackward(
                    streamId,
                    -1,
                    1,
                    false,
                    principal,
                    completed =>
                    {
                        switch (completed.Result)
                        {
                            case ReadStreamResult.Success:
                            case ReadStreamResult.NoStream:
                                if (completed.Events != null && completed.Events.Length > 0)
                                    WriteEvents(streamId, expectedVersion, events, principal, action);
                                else
                                    UpdateStreamAcl(
                                        streamId,
                                        ExpectedVersion.Any,
                                        principal,
                                        streamMetadata.Value,
                                        metaCompleted =>
                                            WriteEvents(streamId, expectedVersion, events, principal, action));
                                break;
                            case ReadStreamResult.AccessDenied:
                                action(
                                    new ClientMessage.WriteEventsCompleted(
                                        Guid.NewGuid(),
                                        OperationResult.AccessDenied,
                                        ""));
                                break;
                            case ReadStreamResult.StreamDeleted:
                                action(
                                    new ClientMessage.WriteEventsCompleted(
                                        Guid.NewGuid(),
                                        OperationResult.StreamDeleted,
                                        ""));
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                    });
        }

        public Guid WriteEvents(
            string streamId,
            long expectedVersion,
            Event[] events,
            IPrincipal principal,
            Action<ClientMessage.WriteEventsCompleted> action)
        {
            var corrId = Guid.NewGuid();
            return
                Writer.Publish(
                    new ClientMessage.WriteEvents(
                        corrId,
                        corrId,
                        Writer.Envelope,
                        false,
                        streamId,
                        expectedVersion,
                        events,
                        principal),
                    action);
        }

        public Guid WriteEvent(
            string streamId,
            long expectedVersion,
            Event @event,
            IPrincipal principal,
            Action<ClientMessage.WriteEventsCompleted> action)
        {
            var corrId = Guid.NewGuid();
            return
                Writer.Publish(
                    new ClientMessage.WriteEvents(
                        corrId,
                        corrId,
                        Writer.Envelope,
                        false,
                        streamId,
                        expectedVersion,
                        new[] {@event},
                        principal),
                    action);
        }

        public Guid DeleteStream(
            string streamId,
            long expectedVersion,
            bool hardDelete,
            IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> action)
        {
            var corrId = Guid.NewGuid();
            return StreamDeleter.Publish(
                new ClientMessage.DeleteStream(
                    corrId,
                    corrId,
                    Writer.Envelope,
                    false,
                    streamId,
                    expectedVersion,
                    hardDelete,
                    principal),
                action);
        }

        public void SubscribeAwake(
            string streamId,
            TFPos from,
            Action<IODispatcherDelayedMessage> action,
            Guid? correlationId = null)
        {
            var corrId = correlationId ?? Guid.NewGuid();
            Awaker.Publish(
                new AwakeServiceMessage.SubscribeAwake(
                    Awaker.Envelope,
                    corrId,
                    streamId,
                    from,
                    new IODispatcherDelayedMessage(corrId, null)),
                action);
        }

        public void UnsubscribeAwake(Guid correlationId)
        {
            Awaker.Cancel(correlationId);
        }

        public void UpdateStreamAcl(
            string streamId,
            long expectedVersion,
            IPrincipal principal,
            StreamMetadata metadata,
            Action<ClientMessage.WriteEventsCompleted> completed)
        {
            WriteEvents(
                SystemStreams.MetastreamOf(streamId),
                expectedVersion,
                new[] {new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata.ToJsonBytes(), null)},
                principal,
                completed);
        }

        public void Delay(TimeSpan delay, Action action)
        {
            _publisher.Publish(
                TimerMessage.Schedule.Create(
                    delay,
                    _inputQueueEnvelope,
                    new IODispatcherDelayedMessage(_selfId, action)));
        }

        public void Handle(IODispatcherDelayedMessage message)
        {
            if (_selfId != message.CorrelationId)
                return;
            message.Action();
        }
    }
}
