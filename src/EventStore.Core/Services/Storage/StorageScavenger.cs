using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Protocol;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Synchronization;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;
using JetBrains.Annotations;
using Serilog;

namespace EventStore.Core.Services.Storage {
	// This tracks the current scavenge and starts/stops/creates it according to the client instructions
	public class StorageScavenger :
		IHandle<ClientMessage.ScavengeDatabase>,
		IHandle<ClientMessage.StopDatabaseScavenge>,
		IHandle<ClientMessage.GetDatabaseScavenge>,
		IHandle<SystemMessage.StateChangeMessage> {

		protected static ILogger Log { get; } = Serilog.Log.ForContext<StorageScavenger>();
		private readonly ITFChunkScavengerLogManager _logManager;
		private readonly ScavengerFactory _scavengerFactory;
		private readonly SemaphoreSlimLock _switchChunksLock;
		private readonly IODispatcher _ioDispatcher;
		private readonly IPublisher _publisher;
		private readonly string _nodeEndpoint;
		private Guid _switchChunksLockId = Guid.Empty;
		private readonly object _lock = new object();
		private readonly IClient _client;

		private IScavenger _currentScavenge;

		// invariant: _currentScavenge is not null => _currentScavengeTask is the task of the current scavenge
		private Task _currentScavengeTask;
		private CancellationTokenSource _cancellationTokenSource;
		private AutomatedScavengeState _autoScavengeState;
		[CanBeNull] private ScavengeConfiguration _scavengeConfiguration;

		public StorageScavenger(
			ITFChunkScavengerLogManager logManager,
			ScavengerFactory scavengerFactory,
			SemaphoreSlimLock switchChunksLock,
			IODispatcher ioDispatcher,
			IPublisher publisher,
			string nodeEndpoint) {

			Ensure.NotNull(logManager, nameof(logManager));
			Ensure.NotNull(scavengerFactory, nameof(scavengerFactory));
			Ensure.NotNull(switchChunksLock, nameof(switchChunksLock));
			Ensure.NotNull(ioDispatcher, nameof(ioDispatcher));
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNullOrEmpty(nodeEndpoint, nameof(nodeEndpoint));

			_logManager = logManager;
			_scavengerFactory = scavengerFactory;
			_switchChunksLock = switchChunksLock;
			_ioDispatcher = ioDispatcher;
			_publisher = publisher;
			_nodeEndpoint = nodeEndpoint;
			_autoScavengeState = new AutomatedScavengeState();
			_client = new InternalClient(publisher);
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			switch (message.State)
			{
				case VNodeState.Follower when _autoScavengeState.Resigning:
					_autoScavengeState.State = VNodeState.Follower;
					OnScavengeScheduled();
					break;

				case VNodeState.Leader or VNodeState.Follower:
					_autoScavengeState.State = message.State;
					_logManager.Initialise();
					ReloadConfiguration();
					break;
			}
		}

		public void Handle(ClientMessage.ScavengeDatabase message) {
			if (IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
				lock (_lock) {
					if (_currentScavenge != null) {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseInProgressResponse(
							message.CorrelationId,
							_currentScavenge.ScavengeId,
							"Scavenge is already running"));
					} else if (!_switchChunksLock.TryAcquire(out _switchChunksLockId)) {
						Log.Information("SCAVENGING: Failed to acquire the chunks lock");
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseInProgressResponse(
							message.CorrelationId,
							Guid.Empty.ToString(),
							"Failed to acquire the chunk switch lock"));
					} else {
						Log.Information("SCAVENGING: Acquired the chunks lock");
						var tfChunkScavengerLog = _logManager.CreateLog();
						var logger = Log.ForContext("ScavengeId", tfChunkScavengerLog.ScavengeId);

						_cancellationTokenSource = new CancellationTokenSource();

						_currentScavenge = _scavengerFactory.Create(message, tfChunkScavengerLog, logger);
						_currentScavengeTask = _currentScavenge.ScavengeAsync(_cancellationTokenSource.Token);

						HandleCleanupWhenFinished(_currentScavengeTask, _currentScavenge, logger);

						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseStartedResponse(
							message.CorrelationId,
							tfChunkScavengerLog.ScavengeId));
					}
				}
			}
		}

		public void Handle(ClientMessage.StopDatabaseScavenge message) {
			if (IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
				lock (_lock) {
					if (_currentScavenge != null &&
					    (_currentScavenge.ScavengeId == message.ScavengeId || message.ScavengeId == "current")) {
						_cancellationTokenSource.Cancel();

						_currentScavengeTask.ContinueWith(_ => {
							message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseStoppedResponse(
								message.CorrelationId,
								_currentScavenge.ScavengeId));
						});
					} else {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseNotFoundResponse(
							message.CorrelationId,
							_currentScavenge?.ScavengeId, "Scavenge Id does not exist"));
					}
				}
			}
		}

		public void Handle(ClientMessage.GetDatabaseScavenge message) {
			if (IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
				lock (_lock) {
					if (_currentScavenge != null) {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseGetResponse(
							message.CorrelationId,
							ClientMessage.ScavengeDatabaseGetResponse.ScavengeResult.InProgress,
							_currentScavenge.ScavengeId));
					} else {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseGetResponse(
							message.CorrelationId, ClientMessage.ScavengeDatabaseGetResponse.ScavengeResult.Stopped,
							scavengeId: null));
					}
				}
			}
		}

		private void ReloadConfiguration() {
			if (_autoScavengeState.State != VNodeState.Leader)
				return;

			ReadBackwards(SystemStreams.ScavengeConfigurationStream, -1, 1, false, OnReadScavengeConfiguration);
		}

		private async void HandleCleanupWhenFinished(Task newScavengeTask, IScavenger newScavenge, ILogger logger) {
			// Clean up the reference to the TfChunkScavenger once it's finished.
			try {
				await newScavengeTask;
			} catch (Exception ex) {
				logger.Error(ex, "SCAVENGING: Unexpected error when scavenging");
			} finally {
				try {
					newScavenge.Dispose();
				} catch (Exception ex) {
					logger.Error(ex, "SCAVENGING: Unexpected error when disposing the scavenger");
				}
			}

			Guid switchChunksLockId;
			lock (_lock) {
				switchChunksLockId = _switchChunksLockId;
			}

			try {
				if (_switchChunksLock.TryRelease(switchChunksLockId)) {
					logger.Information("SCAVENGING: Released the chunks lock");
				} else {
					logger.Information("SCAVENGING: Failed to release the chunks lock");
				}
			} catch (Exception ex) {
				logger.Error(ex, "SCAVENGING: Unexpected error when releasing the chunks lock");
			}

			lock (_lock) {
				if (newScavenge == _currentScavenge) {
					_currentScavenge = null;
				}
			}
		}

		private bool IsAllowed(ClaimsPrincipal user, Guid correlationId, IEnvelope envelope) {
			if (user == null || (!user.LegacyRoleCheck(SystemRoles.Admins) &&
			                     !user.LegacyRoleCheck(SystemRoles.Operations))) {
				envelope.ReplyWith(
					new ClientMessage.ScavengeDatabaseUnauthorizedResponse(correlationId, null, "User not authorized"));
				return false;
			}

			return true;
		}

		private void ReadBackwards(
			string streamName,
			long from,
			int maxCount,
			bool resolveLink,
			Action<ClientMessage.ReadStreamEventsBackwardCompleted> iteratee) {
			ReadBackwards(streamName, from, maxCount, resolveLink, result => {
				iteratee(result);
				return null;
			});
		}

		// Utility function to easily loop over a stream in the forwards direction.
		private void ReadBackwards(
			string streamName,
			long from,
			int maxCount,
			bool resolveLink,
			Func<ClientMessage.ReadStreamEventsBackwardCompleted, long?> iteratee) {

			_ioDispatcher.ReadBackward(streamName, from, maxCount, resolveLink, SystemAccounts.System, result => {
				if (result.Result is ReadStreamResult.Success or ReadStreamResult.NoStream) {
					var next = iteratee(result);

					if (!next.HasValue)
						return;

					ReadBackwards(streamName, next.Value, maxCount, resolveLink, iteratee);
				}

				// We simply retry the same request if an error happened.
				// TODO - Should we wait for some time before re-issuing?
				ReadBackwards(streamName, from, maxCount, resolveLink, iteratee);
			});
		}

		// Utility function to easily loop over a stream in the forwards direction.
		private void ReadForwards(
			string streamName,
			long from,
			int maxCount,
			bool resolveLink,
			Func<ClientMessage.ReadStreamEventsForwardCompleted, long?> iteratee) {

			_ioDispatcher.ReadForward(streamName, from, maxCount, resolveLink, SystemAccounts.System, result => {
				if (result.Result is ReadStreamResult.Success or ReadStreamResult.NoStream) {
					var next = iteratee(result);

					if (!next.HasValue)
						return;

					ReadForwards(streamName, next.Value, maxCount, resolveLink, iteratee);
				}

				// We simply retry the same request if an error happened.
				// TODO - Should we wait for some time before re-issuing?
				ReadForwards(streamName, from, maxCount, resolveLink, iteratee);
			});
		}

		private void OnReadScavengeConfiguration(ClientMessage.ReadStreamEventsBackwardCompleted result) {
			if (_autoScavengeState.State != VNodeState.Leader)
				return;

			if (result.Events.Length == 1)
				_scavengeConfiguration = result.Events[0].OriginalEvent.Data.ParseJson<ScavengeConfiguration>();

			ReadForwards(SystemStreams.ScavengesStream, _autoScavengeState.From, 500, true, OnReadPastScavenges);
		}

		// TODO - We should probably also track if we already have a scavenge running in other nodes. Checking is only
		// eventually consistent because it's possible that our node is lagging behind on replication and we just didn't
		// receive the scavengeStarted event.
		private long? OnReadPastScavenges(ClientMessage.ReadStreamEventsForwardCompleted result) {
			if (_autoScavengeState.State != VNodeState.Leader)
				return null;

			foreach (var @event in result.Events) {
				if (@event.ResolveResult != ReadEventResult.Success)
					continue;

				var dictionary = @event.Event.Data.ParseJson<Dictionary<string, object>>();
				if (!dictionary.TryGetValue("nodeEndpoint", out var entryNode) ||
				    entryNode.ToString() != _nodeEndpoint) {
					continue;
				}


				if (!dictionary.TryGetValue("scavengeId", out var scavengeIdEntry)) {
					Log.Warning("An entry in the scavenge log has no scavengeId");
					continue;
				}

				var scavengeId = scavengeIdEntry.ToString();
				switch (@event.OriginalEvent.EventType) {
					case SystemEventTypes.ScavengeStarted:
						_autoScavengeState.IncompleteScavenges.Add(scavengeId, @event.OriginalEvent.TimeStamp);
						break;

					case SystemEventTypes.ScavengeCompleted: {

						if (!dictionary.TryGetValue("timeTaken", out var timeTakenEntry))
							continue;

						if (!_autoScavengeState.IncompleteScavenges.Remove(scavengeId, out var started))
							continue;

						_autoScavengeState.LastCompletedScavengeStarted = started;
						_autoScavengeState.LastCompleteScavengeTimeTaken = TimeSpan.Parse(timeTakenEntry.ToString());
						break;
					}
				}
			}

			_autoScavengeState.From = result.NextEventNumber;
			if (!result.IsEndOfStream)
				return result.NextEventNumber;

			// Means we either have ongoing scavenges or we don't have any scavenge schedule in place.
			if (_autoScavengeState.IncompleteScavenges.Count != 0 || _scavengeConfiguration == null) {
				// We reload the configuration at a later time to see if some progress was made.
				_publisher.Publish(TimerMessage.Schedule.Create<Message>(TimeSpan.FromSeconds(30),
					new CallbackEnvelope(_ => ReloadConfiguration()), null));

				return null;
			}

			TimeSpan scheduleScavenge = TimeSpan.Zero;

			if (_autoScavengeState.LastCompletedScavengeStarted != null) {
				var ended = _autoScavengeState.LastCompletedScavengeStarted.Value +
				            _autoScavengeState.LastCompleteScavengeTimeTaken;

				var diff = DateTime.Now - ended;
				if (diff >= _scavengeConfiguration.Schedule) {
					OnScavengeScheduled();
					return null;
				}

				scheduleScavenge = _scavengeConfiguration.Schedule - diff;
			} else {
				scheduleScavenge = _scavengeConfiguration.Schedule;
			}

			_publisher.Publish(TimerMessage.Schedule.Create<Message>(scheduleScavenge,
				new CallbackEnvelope(_ => OnScavengeScheduled()), null));

			return null;
		}

		private void OnScavengeScheduled() {
			if (_autoScavengeState.State != VNodeState.Leader)
				return;

			_autoScavengeState.Resigning = true;
			_publisher.Publish(new ClientMessage.ResignNode());

			Handle(new ClientMessage.ScavengeDatabase(new CallbackEnvelope(OnScavengeSubmitted), Guid.NewGuid(),
				SystemAccounts.System, 0, 1, null, null, false));
		}

		private void OnScavengeSubmitted(Message message) {
			switch (message)
			{
				case ClientMessage.ScavengeDatabaseInProgressResponse resp:
					// TODO - Is it worth pulling the starting date from the DB? Right now I say no.
					// TODO - If we reached the head of the scavenges stream then we can set the last completed date ot the moment we received the completion.
					return;
				case ClientMessage.ScavengeDatabaseStartedResponse resp:
					_autoScavengeState.IncompleteScavenges.Add(resp.ScavengeId, DateTime.Now);
					return;
			}
		}


		private struct AutomatedScavengeState() {
			internal VNodeState State = VNodeState.Unknown;
			internal bool Resigning;
			internal long From = 0;
			internal Dictionary<string, DateTime> IncompleteScavenges = new();
			internal DateTime? LastCompletedScavengeStarted;
			internal TimeSpan LastCompleteScavengeTimeTaken = TimeSpan.Zero;
		}
	}
}
