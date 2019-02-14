using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.VNode;

namespace EventStore.Core.Services {
	public class RequestForwardingService : IHandle<SystemMessage.SystemStart>,
		IHandle<SystemMessage.RequestForwardingTimerTick>,
		IHandle<ClientMessage.NotHandled>,
		IHandle<ClientMessage.WriteEventsCompleted>,
		IHandle<ClientMessage.TransactionStartCompleted>,
		IHandle<ClientMessage.TransactionWriteCompleted>,
		IHandle<ClientMessage.TransactionCommitCompleted>,
		IHandle<ClientMessage.DeleteStreamCompleted> {
		private readonly IPublisher _bus;
		private readonly MessageForwardingProxy _forwardingProxy;

		private readonly TimerMessage.Schedule _tickScheduleMessage;

		public RequestForwardingService(IPublisher bus, MessageForwardingProxy forwardingProxy, TimeSpan tickInterval) {
			Ensure.NotNull(bus, "bus");
			Ensure.NotNull(forwardingProxy, "forwardingProxy");
			Ensure.Nonnegative(tickInterval.Milliseconds, "tickInterval");

			_bus = bus;
			_forwardingProxy = forwardingProxy;

			_tickScheduleMessage = TimerMessage.Schedule.Create(tickInterval,
				new PublishEnvelope(bus, crossThread: true),
				new SystemMessage.RequestForwardingTimerTick());
		}

		public void Handle(SystemMessage.SystemStart message) {
			_bus.Publish(_tickScheduleMessage);
		}

		public void Handle(SystemMessage.RequestForwardingTimerTick message) {
			_forwardingProxy.TimeoutForwardings();
			_bus.Publish(_tickScheduleMessage);
		}

		public void Handle(ClientMessage.NotHandled message) {
			_forwardingProxy.TryForwardReply(
				message.CorrelationId, message,
				(clientCorrId, m) => new ClientMessage.NotHandled(clientCorrId, m.Reason, m.AdditionalInfo));
		}

		public void Handle(ClientMessage.WriteEventsCompleted message) {
			_forwardingProxy.TryForwardReply(message.CorrelationId, message,
				(clientCorrId, m) => m.WithCorrelationId(clientCorrId));
		}

		public void Handle(ClientMessage.TransactionStartCompleted message) {
			_forwardingProxy.TryForwardReply(message.CorrelationId, message,
				(clientCorrId, m) => m.WithCorrelationId(clientCorrId));
		}

		public void Handle(ClientMessage.TransactionWriteCompleted message) {
			_forwardingProxy.TryForwardReply(message.CorrelationId, message,
				(clientCorrId, m) => m.WithCorrelationId(clientCorrId));
		}

		public void Handle(ClientMessage.TransactionCommitCompleted message) {
			_forwardingProxy.TryForwardReply(message.CorrelationId, message,
				(clientCorrId, m) => m.WithCorrelationId(clientCorrId));
		}

		public void Handle(ClientMessage.DeleteStreamCompleted message) {
			_forwardingProxy.TryForwardReply(message.CorrelationId, message,
				(clientCorrId, m) => m.WithCorrelationId(clientCorrId));
		}
	}
}
