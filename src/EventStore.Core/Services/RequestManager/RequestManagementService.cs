using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.RequestManager
{
    public interface IRequestManager: IHandle<StorageMessage.RequestManagerTimerTick>
    {
    }

    public class RequestManagementService : IHandle<SystemMessage.SystemInit>,
                                            IHandle<ClientMessage.WriteEvents>, 
                                            IHandle<ClientMessage.DeleteStream>,
                                            IHandle<ClientMessage.TransactionStart>,
                                            IHandle<ClientMessage.TransactionWrite>,
                                            IHandle<ClientMessage.TransactionCommit>,
                                            IHandle<StorageMessage.RequestCompleted>,
                                            IHandle<StorageMessage.CheckStreamAccessCompleted>,
                                            IHandle<StorageMessage.AlreadyCommitted>,
                                            IHandle<StorageMessage.PrepareAck>,
                                            IHandle<StorageMessage.CommitAck>,
                                            IHandle<StorageMessage.WrongExpectedVersion>,
                                            IHandle<StorageMessage.InvalidTransaction>,
                                            IHandle<StorageMessage.StreamDeleted>,
                                            IHandle<StorageMessage.RequestManagerTimerTick>
    {
        private readonly IPublisher _bus;
        private readonly TimerMessage.Schedule _tickRequestMessage;
        private readonly Dictionary<Guid, IRequestManager> _currentRequests = new Dictionary<Guid, IRequestManager>();

        private readonly int _prepareCount;
        private readonly int _commitCount;
        private readonly TimeSpan _prepareTimeout;
        private readonly TimeSpan _commitTimeout;

        public RequestManagementService(IPublisher bus, int prepareCount, int commitCount, TimeSpan prepareTimeout, TimeSpan commitTimeout)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.Nonnegative(prepareCount, "prepareCount");
            Ensure.Nonnegative(commitCount, "commitCount");

            _bus = bus;
            _tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
                                                               new PublishEnvelope(bus),
                                                               new StorageMessage.RequestManagerTimerTick());

            _prepareCount = prepareCount;
            _commitCount = commitCount;
            _prepareTimeout = prepareTimeout;
            _commitTimeout = commitTimeout;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            _bus.Publish(_tickRequestMessage);   
        }

        public void Handle(ClientMessage.WriteEvents message)
        {
            var manager = new WriteStreamTwoPhaseRequestManager(_bus, _prepareCount, _commitCount, _prepareTimeout, _commitTimeout);
            _currentRequests.Add(message.InternalCorrId, manager);
            manager.Handle(message);
        }

        public void Handle(ClientMessage.DeleteStream message)
        {
            var manager = new DeleteStreamTwoPhaseRequestManager(_bus, _prepareCount, _commitCount, _prepareTimeout, _commitTimeout);
            _currentRequests.Add(message.InternalCorrId, manager);
            manager.Handle(message);
        }

        public void Handle(ClientMessage.TransactionStart message)
        {
            var manager = new SingleAckRequestManager(_bus, _prepareTimeout);
            _currentRequests.Add(message.InternalCorrId, manager);
            manager.Handle(message);
        }
        
        public void Handle(ClientMessage.TransactionWrite message)
        {
            var manager = new SingleAckRequestManager(_bus, _prepareTimeout);
            _currentRequests.Add(message.InternalCorrId, manager);
            manager.Handle(message);
        }

        public void Handle(ClientMessage.TransactionCommit message)
        {
            var manager = new TransactionCommitTwoPhaseRequestManager(_bus, _prepareCount, _commitCount, _prepareTimeout, _commitTimeout);
            _currentRequests.Add(message.InternalCorrId, manager);
            manager.Handle(message);
        }

        public void Handle(StorageMessage.RequestCompleted message)
        {
            if (!_currentRequests.Remove(message.CorrelationId))
                throw new InvalidOperationException("Should never complete request twice.");
        }

        public void Handle(StorageMessage.CheckStreamAccessCompleted message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.AlreadyCommitted message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.PrepareAck message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.CommitAck message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.WrongExpectedVersion message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.InvalidTransaction message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.StreamDeleted message)
        {
            DispatchInternal(message.CorrelationId, message);
        }

        public void Handle(StorageMessage.RequestManagerTimerTick message)
        {
            foreach (var currentRequest in _currentRequests)
            {
                currentRequest.Value.Handle(message);
            }
            _bus.Publish(_tickRequestMessage);
        }

        private void DispatchInternal<T>(Guid correlationId, T message) where T : Message
        {
            IRequestManager manager;
            if (_currentRequests.TryGetValue(correlationId, out manager))
            {
                var x = manager as IHandle<T>;
                if (x != null)
                {
                    // message received for a dead request?
                    x.Handle(message);
                }
            }
        }
    }
}
