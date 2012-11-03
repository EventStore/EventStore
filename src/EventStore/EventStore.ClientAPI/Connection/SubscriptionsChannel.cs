using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.Connection
{
    internal class SubscriptionsChannel
    {
        private readonly ILogger _log;

        private readonly TcpConnector _connector;
        private TcpTypedConnection _connection;

        private readonly ManualResetEvent _connectedEvent = new ManualResetEvent(false);
        private readonly object _subscriptionChannelLock = new object();

        private Thread _executionThread;
        private volatile bool _stopExecutionThread;
        private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>(); 

        private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new ConcurrentDictionary<Guid, Subscription>();

        public SubscriptionsChannel(TcpConnector connector)
        {
            _connector = connector;
            _log = LogManager.GetLogger();
        }

        public void Close(bool stopBackgroundThread = true)
        {
            lock (_subscriptionChannelLock)
            {
                if (_connection != null)
                    _connection.Close();

                _stopExecutionThread = stopBackgroundThread;
            }
        }

        public Task Subscribe(string stream, Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, stream, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessages.SubscribeToStream(stream);
                var pkg = new TcpPackage(TcpCommand.SubscribeToStream, id, subscribe.Serialize());
                _connection.EnqueueSend(pkg.AsByteArray());
            }
            else
            {
                source.SetException(new Exception("Failed to add subscription. Concurrency failure."));
            }

            return source.Task;
        }

        public void Unsubscribe(string stream)
        {
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == stream).Select(s => s.Id);

            foreach (var id in ids)
            {
                Subscription removed;
                if(_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    var pkg = new TcpPackage(TcpCommand.UnsubscribeFromStream,
                                             id,
                                             new ClientMessages.UnsubscribeFromStream(stream).Serialize());
                    _connection.EnqueueSend(pkg.AsByteArray());
                }
            }
        }

        public Task SubscribeToAllStreams(Action<RecordedEvent, Position> eventAppeared, Action subscriptionDropped)
        {
            var id = Guid.NewGuid();
            var source = new TaskCompletionSource<object>();

            if (_subscriptions.TryAdd(id, new Subscription(source, id, eventAppeared, subscriptionDropped)))
            {
                var subscribe = new ClientMessages.SubscribeToAllStreams();
                var pkg = new TcpPackage(TcpCommand.SubscribeToAllStreams, id, subscribe.Serialize());
                _connection.EnqueueSend(pkg.AsByteArray());
            }
            else
            {
                source.SetException(new Exception("Failed to add subscription to all streams. Concurrency failure"));
            }
            
            return source.Task;
        }

        public void UnsubscribeFromAllStreams()
        {
            var all = _subscriptions.Values;
            var ids = all.Where(s => s.Stream == null).Select(s => s.Id);

            foreach (var id in ids)
            {
                Subscription removed;
                if (_subscriptions.TryRemove(id, out removed))
                {
                    removed.Source.SetResult(null);
                    ExecuteUserCallbackAsync(removed.SubscriptionDropped);

                    var pkg = new TcpPackage(TcpCommand.UnsubscribeFromAllStreams, 
                                             id, 
                                             new ClientMessages.UnsubscribeFromAllStreams().Serialize());
                    _connection.EnqueueSend(pkg.AsByteArray());
                }
            }
        }

        private void ExecuteUserCallbackAsync(Action callback)
        {
            _executionQueue.Enqueue(callback);
        }

        private void ExecuteUserCallbacks()
        {
            while (!_stopExecutionThread)
            {
                Action callback;
                if (_executionQueue.TryDequeue(out callback))
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "User callback thrown");
                    }
                }
                else
                    Thread.Sleep(1);
            }
        }

        private void OnPackageReceived(TcpTypedConnection connection, TcpPackage package)
        {
            Subscription subscription;
            if(!_subscriptions.TryGetValue(package.CorrelationId, out subscription))
            {
                _log.Error("Unexpected package received : {0} ({1})", package.CorrelationId, package.Command);
                return;
            }

            try
            {
                switch (package.Command)
                {
                    case TcpCommand.StreamEventAppeared:
                        var dto = package.Data.Deserialize<ClientMessages.StreamEventAppeared>();
                        var recordedEvent = new RecordedEvent(dto);
                        var commitPos = dto.CommitPosition;
                        var preparePos = dto.PreparePosition;
                        ExecuteUserCallbackAsync(() => subscription.EventAppeared(recordedEvent, new Position(commitPos, preparePos)));
                        break;
                    case TcpCommand.SubscriptionDropped:
                    case TcpCommand.SubscriptionToAllDropped:
                        Subscription removed;
                        if(_subscriptions.TryRemove(subscription.Id, out removed))
                        {
                            removed.Source.SetResult(null);
                            ExecuteUserCallbackAsync(removed.SubscriptionDropped);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected command : {0}", package.Command));
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "Error on package received");
            }
        }

        private void OnConnectionEstablished(TcpTypedConnection tcpTypedConnection)
        {
            _connectedEvent.Set();
        }

        private void OnConnectionClosed(TcpTypedConnection connection, IPEndPoint endPoint, SocketError error)
        {
            _connectedEvent.Reset();

            var subscriptions = _subscriptions.Values;
            _subscriptions.Clear();

            foreach (var subscription in subscriptions)
            {
                subscription.Source.SetResult(null);
                ExecuteUserCallbackAsync(subscription.SubscriptionDropped);
            }
        }

        private void Connect(IPEndPoint endPoint)
        {
            _connection = _connector.CreateTcpConnection(endPoint, OnPackageReceived, OnConnectionEstablished, OnConnectionClosed);

            if (_executionThread == null)
            {
                _stopExecutionThread = false;
                _executionThread = new Thread(ExecuteUserCallbacks)
                {
                    IsBackground = true,
                    Name = "SubscriptionsChannel user callbacks thread"
                };
                _executionThread.Start();
            }
        }

        public void EnsureConnected(IPEndPoint endPoint)
        {
            if (!_connectedEvent.WaitOne(0))
            {
                lock (_subscriptionChannelLock)
                {
                    if (!_connectedEvent.WaitOne(0))
                    {
                        Connect(endPoint);
                        if (!_connectedEvent.WaitOne(500))
                        {
                            _log.Error("Cannot connect to {0}", _connection.EffectiveEndPoint);
                            throw new CannotEstablishConnectionException(string.Format("Cannot connect to {0}.",
                                                                                       _connection.EffectiveEndPoint));
                        }
                    }
                }
            }
        }
    }
}