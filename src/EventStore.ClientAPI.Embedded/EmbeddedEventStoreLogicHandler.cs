using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Services.UserManagement;
using TcpCommand = EventStore.Core.Services.Transport.Tcp.TcpCommand;
using TcpFlags = EventStore.Core.Services.Transport.Tcp.TcpFlags;
using TcpPackage = EventStore.ClientAPI.SystemData.TcpPackage;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedEventStoreLogicHandler : IEventStoreConnectionLogicHandler
    {
        private static readonly TimerTickMessage TimerTickMessage = new TimerTickMessage();

        private readonly IEventStoreConnection _esConnection;
        private readonly IPublisher _publisher;
        private readonly SimpleQueuedHandler _queue = new SimpleQueuedHandler();
        private readonly ConnectionSettings _settings;

        private int _totalOperationCount;
        private readonly ITcpDispatcher _tcpDispatcher;
        private readonly IDictionary<Guid, OperationItem> _activeOperations = new Dictionary<Guid, OperationItem>();

        public EmbeddedEventStoreLogicHandler(IEventStoreConnection esConnection, ConnectionSettings settings,
            IPublisher publisher)
        {
            Ensure.NotNull(esConnection, "esConnection");
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(publisher, "publisher");

            _esConnection = esConnection;
            _settings = settings;
            _publisher = publisher;
            _queue.RegisterHandler<StartOperationMessage>(
                message => EnqueueOperation(new OperationItem(message.Operation, message.MaxRetries, message.Timeout)));

            _queue.RegisterHandler<HandleTcpPackageMessage>(HandleTcpPackage);


            _tcpDispatcher = new ClientTcpDispatcher();

        }

        private void HandleTcpPackage(HandleTcpPackageMessage message)
        {
            OperationItem operationItem;
            if (_activeOperations.TryGetValue(message.Package.CorrelationId, out operationItem))
            {
                var result = operationItem.Operation.InspectPackage(message.Package);
                switch (result.Decision)
                {
                    case InspectionDecision.DoNothing: break;
                    case InspectionDecision.EndOperation:
                        _activeOperations.Remove(message.Package.CorrelationId);
                        break;
                    default: throw new Exception(string.Format("Unknown InspectionDecision: {0}", result.Decision));
                }
            }
        }

        public int TotalOperationCount
        {
            get { return _totalOperationCount; }
        }

        public void EnqueueMessage(Core.Message message)
        {
            //Interlocked.Increment(ref _totalOperationCount);
            if (_settings.VerboseLogging && message != TimerTickMessage) LogDebug("enqueueing message {0}.", message);
            _queue.EnqueueMessage(message);
        }


        public event EventHandler<ClientConnectionEventArgs> Connected;
        public event EventHandler<ClientConnectionEventArgs> Disconnected;
        public event EventHandler<ClientReconnectingEventArgs> Reconnecting;
        public event EventHandler<ClientClosedEventArgs> Closed;
        public event EventHandler<ClientErrorEventArgs> ErrorOccurred;
        public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed;

        private void EnqueueOperation(OperationItem operationItem)
        {
            _activeOperations.Add(operationItem.CorrelationId, operationItem);
            var package = CreateNetworkPackage(operationItem);
            var message = _tcpDispatcher.UnwrapPackage(package, CreateEnvelope(operationItem), SystemAccount.Principal, package.Login, package.Password, null);
            _publisher.Publish(message);
        }

        private IEnvelope CreateEnvelope(OperationItem operationItem)
        {
            return new CallbackEnvelope(message =>
            {
                var package = _tcpDispatcher.WrapMessage(message).Value;

                var networkPackage = new TcpPackage((SystemData.TcpCommand) package.Command,
                    (SystemData.TcpFlags) package.Flags, package.CorrelationId, package.Login, package.Password,
                    package.Data);

                _queue.EnqueueMessage(new HandleTcpPackageMessage(null, networkPackage));
            });
        }

        private static EventStore.Core.Services.Transport.Tcp.TcpPackage CreateNetworkPackage(OperationItem operationItem)
        {
            TcpPackage networkPackage = operationItem.Operation.CreateNetworkPackage(operationItem.CorrelationId);
            return new EventStore.Core.Services.Transport.Tcp.TcpPackage(
                (TcpCommand) networkPackage.Command,
                (TcpFlags) networkPackage.Flags, networkPackage.CorrelationId, networkPackage.Login, networkPackage.Password,
                networkPackage.Data);
        }

        private void LogDebug(string message, params object[] parameters)
        {
            if (_settings.VerboseLogging)
                _settings.Log.Debug("EventStoreConnection '{0}': {1}.", _esConnection.ConnectionName,
                    parameters.Length == 0 ? message : string.Format(message, parameters));
        }
    }
}