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
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

namespace EventStore.Core.Services.RequestManager {
	public interface ICommitSource {
		long CommitPosition { get; }
		long LogCommitPosition { get; }

		void NotifyCommitFor(long postition, Action target);
		void NotifyLogCommitFor(long postition, Action target);
	}
	public class RequestManagementService :
		ICommitSource,
		IHandle<SystemMessage.SystemInit>,
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
		private readonly TimeSpan _prepareTimeout;
		private readonly TimeSpan _commitTimeout;
		private long _committedPosition;
		private long _logCommittedPosition;

		private VNodeState _nodeState;

		public long CommitPosition => Interlocked.Read(ref _committedPosition);

		public long LogCommitPosition => Interlocked.Read(ref _logCommittedPosition);

		public RequestManagementService(IPublisher bus,
			TimeSpan prepareTimeout,
			TimeSpan commitTimeout,
			bool betterOrdering) {
			Ensure.NotNull(bus, "bus");

			_bus = bus;
			_tickRequestMessage = TimerMessage.Schedule.Create(TimeSpan.FromMilliseconds(1000),
				new PublishEnvelope(bus),
				new StorageMessage.RequestManagerTimerTick());

			_prepareTimeout = prepareTimeout;
			_commitTimeout = commitTimeout;
			_betterOrdering = betterOrdering;
		}

		private ConcurrentDictionary<long, List<Action>> _notifyCommit = new ConcurrentDictionary<long, List<Action>>();
		private ConcurrentDictionary<long, List<Action>> _notifyLogCommit = new ConcurrentDictionary<long, List<Action>>();
		public void NotifyCommitFor(long postition, Action target) {
			if (Interlocked.Read(ref _committedPosition) >= postition) { target(); }
			if (!_notifyCommit.TryGetValue(postition, out var actionList)) {
				actionList = new List<Action>();
				_notifyCommit.TryAdd(postition, actionList);
			}
			actionList.Add(target);
		}
		public void NotifyLogCommitFor(long postition, Action target) {
			if (Interlocked.Read(ref _logCommittedPosition) >= postition) { target(); }
			if (!_notifyLogCommit.TryGetValue(postition, out var actionList)) {
				actionList = new List<Action>();
				_notifyLogCommit.TryAdd(postition, actionList);
			}
			actionList.Add(target);
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
								message.Events,
								this);
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
								message.HardDelete,
								this);
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
								message.User,
								this);
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
								message.Events,
								message.TransactionId,
								this);
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
								message.User,
								this);
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
			Interlocked.Exchange(ref _committedPosition, message.LogPosition);
			Notify(_notifyCommit, message.LogPosition);
		}

		public void Handle(CommitMessage.LogCommittedTo message) {
			Interlocked.Exchange(ref _logCommittedPosition, message.LogPosition);
			Notify(_notifyLogCommit, message.LogPosition);
		}

		private void Notify(ConcurrentDictionary<long, List<Action>> dictionary, long logPosition) {
			if (dictionary.IsEmpty) { return; }
			long[] positions;
			lock (dictionary) {
				positions = dictionary.Keys.ToArray();
			}
			Array.Sort(positions);
			for (int i = 0; i < positions.Length && positions[i] <= logPosition; i++) {
				dictionary[positions[i]].ForEach(a => { try { a?.Invoke(); } catch { } });
				dictionary.TryRemove(positions[i], out _);
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
