using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Concurrent;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubscription
    {
        private readonly ILogger _log;
        private readonly IPublisher _publisher;
        private readonly Guid _connectionId;
        private readonly TaskCompletionSource<EventStoreSubscription> _source;
        private readonly string _streamId;
        private readonly bool _resolveLinkTos;
        private readonly Action<EventStoreSubscription, ResolvedEvent> _eventAppeared;
        private readonly Action<EventStoreSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
        private int _actionExecuting;
        private readonly ConcurrentQueue<Action> _actionQueue;
        private EventStoreSubscription _subscription;
        private int _unsubscribed;
        private Guid _correlationId;

        public EmbeddedSubscription(ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<EventStoreSubscription> source, string streamId, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
        {
            Ensure.NotNull(source, "source");
            Ensure.NotNull(streamId, "streamId");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.NotNull(publisher, "publisher");

            _log = log;
            _publisher = publisher;
            _connectionId = connectionId;
            _source = source;
            _streamId = streamId;
            _resolveLinkTos = resolveLinkTos;
            _eventAppeared = eventAppeared;
            _subscriptionDropped = subscriptionDropped ?? ((a, b, c) => { });
            _actionQueue = new ConcurrentQueue<Action>();
        }

        public void DropSubscription(EventStore.Core.Services.SubscriptionDropReason reason)
        {
            switch (reason)
            {
                case EventStore.Core.Services.SubscriptionDropReason.AccessDenied:
                    DropSubscription(SubscriptionDropReason.AccessDenied,
                        new AccessDeniedException(string.Format("Subscription to '{0}' failed due to access denied.",
                            _streamId == string.Empty ? "<all>" : _streamId)));
                    break;
                case EventStore.Core.Services.SubscriptionDropReason.Unsubscribed:
                    Unsubscribe();
                    break;
            }
        }

        public void EventAppeared(EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            _eventAppeared(_subscription, new ResolvedEvent(resolvedEvent.ConvertToResolvedIndexEvent()));
        }


        public void ConfirmSubscription(long lastCommitPosition, int? lastEventNumber)
        {
            if (lastCommitPosition < -1)
                throw new ArgumentOutOfRangeException("lastCommitPosition", string.Format("Invalid lastCommitPosition {0} on subscription confirmation.", lastCommitPosition));
            if (_subscription != null)
                throw new Exception("Double confirmation of subscription.");

            _subscription = new EventStoreSubscription(Unsubscribe, _streamId, lastCommitPosition, lastEventNumber);
            _source.SetResult(_subscription);
        }

        public void Unsubscribe()
        {
            DropSubscription(SubscriptionDropReason.UserInitiated, null);
        }

        private void DropSubscription(SubscriptionDropReason reason, Exception exception)
        {
            if (Interlocked.CompareExchange(ref _unsubscribed, 1, 0) == 0)
            {

                if (reason != SubscriptionDropReason.UserInitiated)
                {
                    if (exception == null) throw new Exception(string.Format("No exception provided for subscription drop reason '{0}", reason));
                    _source.TrySetException(exception);
                }

                if (reason == SubscriptionDropReason.UserInitiated && _subscription != null)
                    _publisher.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), _correlationId, new NoopEnvelope(), SystemAccount.Principal));

                if (_subscription != null)
                    ExecuteActionAsync(() => _subscriptionDropped(_subscription, reason, exception));

            }
        }

        private void ExecuteActionAsync(Action action)
        {
            _actionQueue.Enqueue(action);
            if (Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ExecuteActions);
        }

        private void ExecuteActions(object state)
        {
            do
            {
                Action action;
                while (_actionQueue.TryDequeue(out action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exc)
                    {
                        _log.Error(exc, "Exception during executing user callback: {0}.", exc.Message);
                    }
                }

                Interlocked.Exchange(ref _actionExecuting, 0);
            } while (_actionQueue.Count > 0 && Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0);
        }

        public void Start(Guid correlationId)
        {
            _correlationId = correlationId;

            _publisher.Publish(new ClientMessage.SubscribeToStream(correlationId, correlationId, new PublishEnvelope(_publisher, true), _connectionId, _streamId, _resolveLinkTos, SystemAccount.Principal));
        }
    }
}