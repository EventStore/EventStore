// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations
{
    internal class SubscriptionOperation
    {
        private readonly ILogger _log;
        private readonly TaskCompletionSource<EventStoreSubscription> _source;
        private readonly Action<TcpPackage> _sendPackage;
        private readonly string _streamId;
        private readonly bool _resolveLinkTos;
        private readonly Action<EventStoreSubscription, ResolvedEvent> _eventAppeared;
        private readonly Action<EventStoreSubscription, string, Exception> _subscriptionDropped;

        private readonly Common.Concurrent.ConcurrentQueue<Action> _actionQueue = new Common.Concurrent.ConcurrentQueue<Action>();
        private int _actionExecuting;
        private EventStoreSubscription _subscription;
        private int _unsubscribed;
        private Guid _correlationId;

        public SubscriptionOperation(ILogger log,
                                     TaskCompletionSource<EventStoreSubscription> source,
                                     Action<TcpPackage> sendPackage,
                                     string streamId,
                                     bool resolveLinkTos,
                                     Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                                     Action<EventStoreSubscription, string, Exception> subscriptionDropped)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(sendPackage, "sendPackage");
            Ensure.NotNull(source, "source");
            Ensure.NotNull(eventAppeared, "eventAppeared");

            _log = log;
            _source = source;
            _sendPackage = sendPackage;
            _streamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
            _resolveLinkTos = resolveLinkTos;
            _eventAppeared = eventAppeared;
            _subscriptionDropped = subscriptionDropped ?? ((x, y, z) => { });
        }

        public bool Subscribe(Guid correlationId)
        {
            if (_subscription != null || _unsubscribed != 0)
                return false;

            _correlationId = correlationId;
            _sendPackage(CreateSubscriptionPackage());
            return true;
        }

        private TcpPackage CreateSubscriptionPackage()
        {
            var dto = new ClientMessage.SubscribeToStream(_streamId, _resolveLinkTos);
            return new TcpPackage(TcpCommand.SubscribeToStream, _correlationId, dto.Serialize());
        }

        public void Unsubscribe()
        {
            DropSubscription("Client requested subscription drop.", null, clientInitiated: true, shouldSubscriptionExist: true);
        }

        private TcpPackage CreateUnsubscriptionPackage()
        {
            return new TcpPackage(TcpCommand.UnsubscribeFromStream, _correlationId, new ClientMessage.UnsubscribeFromStream().Serialize());
        }

        public InspectionResult InspectPackage(TcpPackage package)
        {
            try
            {
                switch (package.Command)
                {
                    case TcpCommand.SubscriptionConfirmation:
                    {
                        var dto = package.Data.Deserialize<ClientMessage.SubscriptionConfirmation>();
                        ConfirmSubscription(dto.LastCommitPosition, dto.LastEventNumber);
                        return new InspectionResult(InspectionDecision.DoNothing);
                    }

                    case TcpCommand.StreamEventAppeared:
                    {
                        var dto = package.Data.Deserialize<ClientMessage.StreamEventAppeared>();
                        EventAppeared(new ResolvedEvent(dto.Event));
                        return new InspectionResult(InspectionDecision.DoNothing);
                    }

                    case TcpCommand.SubscriptionDropped:
                    {
                        DropSubscription("Subscription dropped by server.", null, clientInitiated: false, shouldSubscriptionExist: true);
                        return new InspectionResult(InspectionDecision.EndOperation);
                    }

                    case TcpCommand.BadRequest:
                    {
                        string message = Helper.EatException(() => Encoding.UTF8.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
                        var exc = new ServerErrorException(string.IsNullOrEmpty(message) ? "<no message>" : message);
                        Fail(exc);
                        DropSubscription("Server error.", exc, clientInitiated: false, shouldSubscriptionExist: false);
                        return new InspectionResult(InspectionDecision.EndOperation);
                    }

                    case TcpCommand.NotHandled:
                    {
                        if (_subscription != null)
                            throw new Exception("NotHandled command appeared while we already subscribed.");

                        var message = package.Data.Deserialize<ClientMessage.NotHandled>();
                        switch (message.Reason)
                        {
                            case ClientMessage.NotHandled.NotHandledReason.NotReady:
                            case ClientMessage.NotHandled.NotHandledReason.TooBusy:
                                return new InspectionResult(InspectionDecision.Retry);

                            case ClientMessage.NotHandled.NotHandledReason.NotMaster:
                                var masterInfo = message.AdditionalInfo.Deserialize<ClientMessage.NotHandled.MasterInfo>();
                                return new InspectionResult(InspectionDecision.Reconnect, masterInfo.ExternalTcpEndPoint);

                            default:
                                _log.Info("Unknown NotHandledReason: {0}.", message.Reason);
                                return new InspectionResult(InspectionDecision.Retry);
                        }
                    }

                    default:
                    {
                        var exc = new CommandNotExpectedException(package.Command.ToString());
                        Fail(exc);
                        DropSubscription("Unrecognized package error.", exc, clientInitiated: false, shouldSubscriptionExist: false);
                        return new InspectionResult(InspectionDecision.EndOperation);
                    }
                }
            }
            catch (Exception e)
            {
                Fail(e);
                return new InspectionResult(InspectionDecision.EndOperation);
            }
        }

        internal void ConnectionClosed()
        {
            DropSubscription("Connection was closed.", null, clientInitiated: false, shouldSubscriptionExist: false);
        }

        internal bool TimeOutSubscription()
        {
            if (_subscription != null)
                return false;
            DropSubscription("Subscribing timed out.", null, clientInitiated: false, shouldSubscriptionExist: false);
            return true;
        }

        private void DropSubscription(string reason, Exception exc, bool clientInitiated, bool shouldSubscriptionExist)
        {
            if (shouldSubscriptionExist && _subscription == null)
                throw new InvalidOperationException("Subscription was not confirmed, but unsubscribing requested.");

            if (Interlocked.CompareExchange(ref _unsubscribed, 1, 0) == 0)
            {
                _log.Debug("Subscription {0:B} to {1}: closing subscription, reason: {2}...",
                           _correlationId, _streamId == string.Empty ? "<all>" : _streamId, reason);

                if (clientInitiated && _subscription != null)
                    _sendPackage(CreateUnsubscriptionPackage());

                if (_subscription != null)
                    ExecuteActionAsync(() => _subscriptionDropped(_subscription, reason, exc));
            }
        }

        private void ConfirmSubscription(long lastCommitPosition, int? lastEventNumber)
        {
            if (lastCommitPosition < -1)
                throw new ArgumentOutOfRangeException("lastCommitPosition", string.Format("Invalid lastCommitPosition {0} on subscription confirmation.", lastCommitPosition));
            if (_subscription != null) 
                throw new Exception("Double confirmation of subscription.");

            _log.Debug("Subscription {0:B} to {1}: subscribed at CommitPosition: {2}, EventNumber: {3}.",
                       _correlationId, _streamId == string.Empty ? "<all>" : _streamId, lastCommitPosition, lastEventNumber);

            _subscription = new EventStoreSubscription(this, _streamId, lastCommitPosition, lastEventNumber);
            _source.SetResult(_subscription);
        }

        private void EventAppeared(ResolvedEvent e)
        {
            if (_unsubscribed != 0)
                return;

            if (_subscription == null)
                throw new Exception("Subscription not confirmed, but event appeared!");

            _log.Debug("Subscription {0:B} to {1}: event appeared ({2}, {3}, {4} @ {5}).",
                      _correlationId, _streamId == string.Empty ? "<all>" : _streamId,
                      e.OriginalStreamId, e.OriginalEventNumber, e.OriginalEvent.EventType, e.OriginalPosition);

            ExecuteActionAsync(() => _eventAppeared(_subscription, e));
        }

        public void Fail(Exception exception)
        {
            _source.TrySetException(exception);
            DropSubscription("Subscription failed.", exception, clientInitiated: false, shouldSubscriptionExist: false);
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
    }

}