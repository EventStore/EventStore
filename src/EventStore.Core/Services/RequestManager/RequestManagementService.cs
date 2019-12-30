using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Services.TimerService;
using System.Diagnostics;
using EventStore.Core.Data;
using EventStore.Core.Services.Histograms;

namespace EventStore.Core.Services.RequestManager {
	
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
		IHandle<CommitMessage.CommittedTo>,
		IHandle<CommitMessage.LogCommittedTo>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.RequestManagerTimerTick>,
		IHandle<SystemMessage.StateChangeMessage> {
		private readonly IPublisher _bus;
		private readonly TimerMessage.Schedule _tickRequestMessage;
		private readonly Dictionary<Guid, RequestManagerBase> _currentRequests = new Dictionary<Guid, RequestManagerBase>();
		private readonly Dictionary<Guid, Stopwatch> _currentTimedRequests = new Dictionary<Guid, Stopwatch>();
		private const string _requestManagerHistogram = "request-manager";
		private readonly bool _betterOrdering;
		private readonly int _prepareCount;
		private readonly TimeSpan _prepareTimeout;
		private readonly TimeSpan _commitTimeout;
		private VNodeState _nodeState;

		public RequestManagementService(IPublisher bus,
			int prepareCount,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			bool betterOrdering) {
			Ensure.NotNull(bus, "bus");
			Ensure.Nonnegative(prepareCount, "prepareCount");

			_bus = bus;
			_tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
				new PublishEnvelope(bus),
				new StorageMessage.RequestManagerTimerTick());

			_prepareCount = prepareCount;
			_prepareTimeout = prepareTimeout;
			_commitTimeout = commitTimeout;
			_betterOrdering = betterOrdering;
		}

		public void Handle(SystemMessage.SystemInit message) {
			_bus.Publish(_tickRequestMessage);
		}

		public void Handle(ClientMessage.WriteEvents message) {
			var manager = new WriteEvents(
								_bus,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,								
								_betterOrdering,
								message.ExpectedVersion,
								message.User,
								message.Events);
			_currentRequests.Add(message.InternalCorrId, manager);			
			_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			manager.Start();
		}

		public void Handle(ClientMessage.DeleteStream message) {
			var manager = new DeleteStream(
								_bus,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,
								_betterOrdering,
								message.ExpectedVersion,
								message.User,
								message.HardDelete);
			_currentRequests.Add(message.InternalCorrId, manager);
			_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionStart message) {
			var manager = new TransactionStart(
								_bus,
								_prepareTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.EventStreamId,
								_betterOrdering,
								message.ExpectedVersion,
								message.User);
			_currentRequests.Add(message.InternalCorrId, manager);
			_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionWrite message) {
			var manager = new TransactionWrite(
								_bus,
								_prepareTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								_betterOrdering,
								message.Events,
								message.TransactionId);
			_currentRequests.Add(message.InternalCorrId, manager);
			_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			manager.Start();
		}

		public void Handle(ClientMessage.TransactionCommit message) {
			var manager = new TransactionCommit(
								_bus,
								_prepareTimeout,
								_commitTimeout,
								message.Envelope,
								message.InternalCorrId,
								message.CorrelationId,
								message.TransactionId,
								_betterOrdering,
								message.User);
			_currentRequests.Add(message.InternalCorrId, manager);
			_currentTimedRequests.Add(message.InternalCorrId, Stopwatch.StartNew());
			manager.Start();
		}

		public void Handle(StorageMessage.RequestCompleted message) {
			Stopwatch watch = null;
			if (_currentTimedRequests.TryGetValue(message.CorrelationId, out watch)) {
				HistogramService.SetValue(_requestManagerHistogram,
					(long)((((double)watch.ElapsedTicks) / Stopwatch.Frequency) * 1000000000));
				_currentTimedRequests.Remove(message.CorrelationId);
			}
			
			if (!_currentRequests.Remove(message.CorrelationId))
				throw new InvalidOperationException("Should never complete request twice.");
		}

		public void Handle(StorageMessage.CheckStreamAccessCompleted message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.AlreadyCommitted message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.PrepareAck message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.CommitAck message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(CommitMessage.CommittedTo message) {
			foreach (var currentRequest in _currentRequests) {
				currentRequest.Value.Handle(message);
			}
		}
		public void Handle(CommitMessage.LogCommittedTo message) {
			foreach (var currentRequest in _currentRequests) {
				currentRequest.Value.Handle(message);
			}
		}
		public void Handle(StorageMessage.WrongExpectedVersion message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.InvalidTransaction message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.StreamDeleted message) {
			DispatchInternal(message.CorrelationId, message);
		}

		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			foreach (var currentRequest in _currentRequests) {
				currentRequest.Value.Handle(message);
			}
			//todo-clc: if we have become resigning master should all requests be actively disposed?
			if (_nodeState == VNodeState.ResigningMaster && _currentRequests.Count == 0) {
				_bus.Publish(new SystemMessage.RequestQueueDrained());
			}

			_bus.Publish(_tickRequestMessage);
		}

		private void DispatchInternal<T>(Guid correlationId, T message) where T : Message {
			
			if (_currentRequests.TryGetValue(correlationId, out var manager)) {
				var x = manager as IHandle<T>;
				x?.Handle(message);
			}
			//else message received for a dead request? 
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_nodeState = message.State;			
		}
	}
}
