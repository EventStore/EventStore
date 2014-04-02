using System;
using System.Collections.Generic;
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
                    envelope);

        }

        public Guid ReadBackward(
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
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
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsForwardCompleted> action)
        {
            var corrId = Guid.NewGuid();
            return
                ForwardReader.Publish(
                    new ClientMessage.ReadStreamEventsForward(
                        corrId,
                        corrId,
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
            string streamId, int expectedVersion, Lazy<StreamMetadata> streamMetadata, Event[] events,
            IPrincipal principal, Action<ClientMessage.WriteEventsCompleted> action)
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
            string streamId, int expectedVersion, Event[] events, IPrincipal principal,
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
            string streamId, int expectedVersion, Event @event, IPrincipal principal,
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

        public void DeleteStream(
            string streamId, int expectedVersion, bool hardDelete, IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> action)
        {
            var corrId = Guid.NewGuid();
            StreamDeleter.Publish(
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

        public void SubscribeAwake(string streamId, TFPos from, Action<IODispatcherDelayedMessage> action)
        {
            var corrId = Guid.NewGuid();
            Awaker.Publish(
                new AwakeServiceMessage.SubscribeAwake(
                    Awaker.Envelope,
                    corrId,
                    streamId,
                    from,
                    new IODispatcherDelayedMessage(corrId, null)),
                action);
        }

        public void UpdateStreamAcl(
            string streamId, int expectedVersion, IPrincipal principal, StreamMetadata metadata,
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


        public delegate void Step(IEnumerator<Step> nextSteps);

        public void Perform(IEnumerable<Step> actions)
        {
            var actionsEnumerator = actions.GetEnumerator();
            Perform(actionsEnumerator);
        }

        public void Perform(params Step[] actions)
        {
            var actionsEnumerator = ((IEnumerable<Step>)actions).GetEnumerator();
            Perform(actionsEnumerator);
        }

        private void Perform(IEnumerator<Step> actions)
        {
            if (actions.MoveNext())
            {
                var action = actions.Current;
                action(actions);
            }
        }

        public Step BeginReadForward(
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsForwardCompleted> handler)
        {
            return steps => ReadForward(
                streamId,
                fromEventNumber,
                maxCount,
                resolveLinks,
                principal,
                response =>
                {
                    handler(response);
                    Perform(steps);
                });
        }

        public Step BeginReadBackward(
            string streamId, int fromEventNumber, int maxCount, bool resolveLinks, IPrincipal principal,
            Action<ClientMessage.ReadStreamEventsBackwardCompleted> handler)
        {
            return steps => ReadBackward(
                streamId,
                fromEventNumber,
                maxCount,
                resolveLinks,
                principal,
                response =>
                {
                    handler(response);
                    Perform(steps);
                });
        }

        public Step BeginWriteEvents(
            string streamId, int expectedVersion, IPrincipal principal, Event[] events,
            Action<ClientMessage.WriteEventsCompleted> handler)
        {
            return steps => WriteEventsWithRetry(streamId, expectedVersion, principal, events, handler, steps);
        }

        public Step BeginDeleteStream(
            string streamId, int expectedVersion, bool hardDelete, IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> handler)
        {
              return steps => DeleteStreamWithRetry(streamId, expectedVersion, hardDelete, principal, handler, steps);
        }

        public Step BeginSubscribeAwake(string streamId, TFPos from, Action<IODispatcherDelayedMessage> handler)
        {
            return steps => SubscribeAwake(
                streamId,
                from,
                message =>
                {
                    handler(message);
                    Perform(steps);
                });
        }

        private void WriteEventsWithRetry(
            string streamId, int expectedVersion, IPrincipal principal, Event[] events,
            Action<ClientMessage.WriteEventsCompleted> handler, IEnumerator<Step> steps)
        {
            PerformWithRetry(
                handler,
                steps,
                expectedVersion == ExpectedVersion.Any,
                TimeSpan.FromMilliseconds(100),
                action =>
                    WriteEvents(
                        streamId,
                        expectedVersion,
                        events,
                        principal,
                        response => action(response, response.Result)));
        }


        private void DeleteStreamWithRetry(
            string streamId, int expectedVersion, bool hardDelete, IPrincipal principal,
            Action<ClientMessage.DeleteStreamCompleted> handler, IEnumerator<Step> steps)
        {
            PerformWithRetry(
                handler,
                steps,
                expectedVersion == ExpectedVersion.Any,
                TimeSpan.FromMilliseconds(100),
                action =>
                    DeleteStream(
                        streamId,
                        expectedVersion,
                        hardDelete,
                        principal,
                        response => action(response, response.Result)));
        }

        private void PerformWithRetry<T>(
            Action<T> handler,
            IEnumerator<Step> steps,
            bool retryExpectedVersion,
            TimeSpan timeout,
            Action<Action<T, OperationResult>> action)
        {
            action(
                (response, result) =>
                {
                    if (ShouldRetry(result, retryExpectedVersion))
                    {
                        Delay(
                            timeout,
                            () =>
                            {
                                if (timeout < TimeSpan.FromSeconds(10))
                                    timeout += timeout;
                                PerformWithRetry(handler, steps, retryExpectedVersion, timeout, action);
                            });
                    }
                    else
                    {
                        handler(response);
                        Perform(steps);
                    }
                });
        }

        private bool ShouldRetry(OperationResult result, bool retryExpectedVersion)
        {
            switch (result)
            {
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.PrepareTimeout:
                    return true;
                case OperationResult.WrongExpectedVersion:
                    return retryExpectedVersion;
                default:
                    return false;
            }
        }


        public Step BeginDelay(TimeSpan timeout, Action handler)
        {
            return steps => Delay(
                timeout,
                () =>
                {
                    handler();
                    Perform(steps);
                });
        }
    }
}
