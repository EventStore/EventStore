using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Concurrent;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a persistent subscription connection.
    /// </summary>
    public class EventStorePersistentSubscription
    {
        private static readonly ResolvedEvent DropSubscriptionEvent = new ResolvedEvent();
        ///<summary>
        ///The default buffer size for the persistent subscription
        ///</summary>
        public const int DefaultBufferSize = 10;

        private readonly string _subscriptionId;
        private readonly string _streamId;
        private readonly Action<EventStorePersistentSubscription, ResolvedEvent> _eventAppeared;
        private readonly Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
        private readonly UserCredentials _userCredentials;
        private readonly ILogger _log;
        private readonly bool _verbose;
        private readonly ConnectionSettings _settings;
        private readonly EventStoreConnectionLogicHandler _handler;
        private readonly bool _autoAck;

        private PersistentEventStoreSubscription _subscription;
        private readonly ConcurrentQueue<ResolvedEvent> _queue = new ConcurrentQueue<ResolvedEvent>();
        private int _isProcessing;
        private DropData _dropData;

        private int _isDropped;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
        private int _bufferSize;

        internal EventStorePersistentSubscription(string subscriptionId, 
            string streamId, 
            Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared, 
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
            UserCredentials userCredentials,
            ILogger log,
            bool verboseLogging,
            ConnectionSettings settings, 
            EventStoreConnectionLogicHandler handler,
            int bufferSize = 10,
            bool autoAck = true)
        {
            _subscriptionId = subscriptionId;
            _streamId = streamId;
            _eventAppeared = eventAppeared;
            _subscriptionDropped = subscriptionDropped;
            _userCredentials = userCredentials;
            _log = log;
            _verbose = verboseLogging;
            _settings = settings;
            _handler = handler;
            _bufferSize = bufferSize;
            _autoAck = autoAck;
        }

        internal void Start()
        {
            _stopped.Reset();

            var source = new TaskCompletionSource<PersistentEventStoreSubscription>();
            _handler.EnqueueMessage(new StartPersistentSubscriptionMessage(source, _subscriptionId, _streamId, _bufferSize,
                                                                 _userCredentials, OnEventAppeared,
                                                                 OnSubscriptionDropped, _settings.MaxRetries, _settings.OperationTimeout));
            source.Task.Wait();
            _subscription = source.Task.Result;
        }


        /// <summary>
        /// Acknowledge that a message have completed processing (this will tell the server it has been processed)
        /// </summary>
        /// <remarks>There is no need to ack a message if you have Auto Ack enabled</remarks>
        /// <param name="event">The <see cref="ResolvedEvent"></see> to acknowledge</param>
        public void Acknowledge(ResolvedEvent @event)
        {
            _subscription.NotifyEventsProcessed(new[] { @event.Event.EventId });
        }

        /// <summary>
        /// Acknowledge that a message have completed processing (this will tell the server it has been processed)
        /// </summary>
        /// <remarks>There is no need to ack a message if you have Auto Ack enabled</remarks>
        /// <param name="events">The <see cref="ResolvedEvent"></see>s to acknowledge there should be less than 2000 to ack at a time.</param>
        public void Acknowledge(IEnumerable<ResolvedEvent> events)
        {
            var ids = events.Select(x => x.Event.EventId).ToArray();
            if(ids.Length > 2000) throw new ArgumentOutOfRangeException("events", "events is limited to 2000 to ack at a time");
            _subscription.NotifyEventsProcessed(ids);
        }

        /// <summary>
        /// Mark a message failed processing. The server will be take action based upon the action paramter
        /// </summary>
        /// <param name="event">The event to mark as failed</param>
        /// <param name="action">The <see cref="PersistentSubscriptionNakEventAction"></see> action to take</param>
        /// <param name="reason">A string with a message as to why the failure is occuring</param>
        public void Fail(ResolvedEvent @event, PersistentSubscriptionNakEventAction action, string reason)
        {
            _subscription.NotifyEventsFailed(new [] {@event.Event.EventId}, action, reason);
        }

        /// <summary>
        /// Mark nmessages that have failed processing. The server will take action based upon the action parameter
        /// </summary>
        /// <param name="events">The events to mark as failed</param>
        /// <param name="action">The <see cref="PersistentSubscriptionNakEventAction"></see> action to take</param>
        /// <param name="reason">A string with a message as to why the failure is occuring</param>
        public void Fail(IEnumerable<ResolvedEvent> events, PersistentSubscriptionNakEventAction action, string reason)
        {
            var ids = events.Select(x => x.Event.EventId).ToArray();
            if (ids.Length > 2000) throw new ArgumentOutOfRangeException("events", "events is limited to 2000 to ack at a time");
            _subscription.NotifyEventsFailed(ids, action, reason);
        }


        /// <summary>
        /// Disconnects this client from the persistent subscriptions.
        /// </summary>
        /// <param name="timeout"></param>
        /// <exception cref="TimeoutException"></exception>
        public void Stop(TimeSpan timeout)
        {
            if (_verbose) _log.Debug("Persistent Subscription to {0}: requesting stop...", _streamId);

            EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
            if (!_stopped.Wait(timeout))
                throw new TimeoutException(string.Format("Couldn't stop {0} in time.", GetType().Name));
        }

        private void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error)
        {
            // if drop data was already set -- no need to enqueue drop again, somebody did that already
            var dropData = new DropData(reason, error);
            if (Interlocked.CompareExchange(ref _dropData, dropData, null) == null)
            {
                Enqueue(DropSubscriptionEvent);
            }
        }

        private void OnSubscriptionDropped(EventStoreSubscription subscription, SubscriptionDropReason reason, Exception exception)
        {
            if (_subscriptionDropped != null)
            {
                _subscriptionDropped(this, reason, exception);
            }
        }

        private void OnEventAppeared(EventStoreSubscription subscription, ResolvedEvent resolvedEvent)
        {
            Enqueue(resolvedEvent);
        }

        private void Enqueue(ResolvedEvent resolvedEvent)
        {
            _queue.Enqueue(resolvedEvent);
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(_ => ProcessQueue());
        }


        private void ProcessQueue()
        {
            do
            {
                ResolvedEvent e;
                while (_queue.TryDequeue(out e))
                {
                    if (e.Event == null) // drop subscription artificial ResolvedEvent
                    {
                        if (_dropData == null) throw new Exception("Drop reason not specified.");
                        DropSubscription(_dropData.Reason, _dropData.Error);
                        return;
                    }
                    if (_dropData != null)
                    {
                        DropSubscription(_dropData.Reason, _dropData.Error);
                        return;
                    }
                    try
                    {
                        _eventAppeared(this, e);
                        if(_autoAck)
                            _subscription.NotifyEventsProcessed(new[]{e.Event.EventId});
                        if (_verbose)
                            _log.Debug("Persistent Subscription to {0}: processed event ({1}, {2}, {3} @ {4}).",
                                      _streamId,
                                      e.OriginalEvent.EventStreamId, e.OriginalEvent.EventNumber, e.OriginalEvent.EventType, e.OriginalEventNumber);
                    }
                    catch (Exception exc)
                    {
                        DropSubscription(SubscriptionDropReason.EventHandlerException, exc);
                        return;
                    }
                }
                Interlocked.CompareExchange(ref _isProcessing, 0, 1);
            } while (_queue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
        }

        
        private void DropSubscription(SubscriptionDropReason reason, Exception error)
        {
            if (Interlocked.CompareExchange(ref _isDropped, 1, 0) == 0)
            {
                if (_verbose)
                    _log.Debug("Persistent Subscription to {0}: dropping subscription, reason: {1} {2}.",
                              _streamId, reason, error == null ? string.Empty : error.ToString());

                if (_subscription != null)
                    _subscription.Unsubscribe();
                if (_subscriptionDropped != null)
                    _subscriptionDropped(this, reason, error);
                _stopped.Set();
            }
        }

        private class DropData
        {
            public readonly SubscriptionDropReason Reason;
            public readonly Exception Error;

            public DropData(SubscriptionDropReason reason, Exception error)
            {
                Reason = reason;
                Error = error;
            }
        }
    }
}
