using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Synchronization;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;
using JetBrains.Annotations;
using Serilog;

namespace EventStore.Core.Services.Storage {
	// This tracks the current scavenge and starts/stops/creates it according to the client instructions
	public class StorageScavenger :
		IHandle<SystemMessage.SystemReady>,
		IHandle<ClientMessage.ScavengeDatabase>,
		IHandle<ClientMessage.StopDatabaseScavenge>,
		IHandle<ClientMessage.GetDatabaseScavenge>,
		IHandle<SystemMessage.StateChangeMessage> {

		protected static ILogger Log { get; } = Serilog.Log.ForContext<StorageScavenger>();
		private readonly ITFChunkScavengerLogManager _logManager;
		private readonly ScavengerFactory _scavengerFactory;
		private readonly SemaphoreSlimLock _switchChunksLock;
		private readonly IODispatcher _ioDispatcher;
		private readonly TimerService.TimerService _timerService;
		private readonly string _nodeEndpoint;
		private Guid _switchChunksLockId = Guid.Empty;
		private readonly object _lock = new object();

		private IScavenger _currentScavenge;

		// invariant: _currentScavenge is not null => _currentScavengeTask is the task of the current scavenge
		private Task _currentScavengeTask;
		private CancellationTokenSource _cancellationTokenSource;
		private bool _initialized;
		private long _from;
		[CanBeNull] private ScavengeConfiguration _scavengeConfiguration;

		public StorageScavenger(
			ITFChunkScavengerLogManager logManager,
			ScavengerFactory scavengerFactory,
			SemaphoreSlimLock switchChunksLock,
			IODispatcher ioDispatcher,
			TimerService.TimerService timerService,
			string nodeEndpoint) {

			Ensure.NotNull(logManager, nameof(logManager));
			Ensure.NotNull(scavengerFactory, nameof(scavengerFactory));
			Ensure.NotNull(switchChunksLock, nameof(switchChunksLock));
			Ensure.NotNull(ioDispatcher, nameof(ioDispatcher));
			Ensure.NotNull(timerService, nameof(timerService));
			Ensure.NotNullOrEmpty(nodeEndpoint, nameof(nodeEndpoint));

			_logManager = logManager;
			_scavengerFactory = scavengerFactory;
			_switchChunksLock = switchChunksLock;
			_ioDispatcher = ioDispatcher;
			_timerService = timerService;
			_nodeEndpoint = nodeEndpoint;
			_from = 0;
		}

		public void Handle(SystemMessage.SystemReady message) {
			if (_initialized)
				return;

			_ioDispatcher.ReadBackward(SystemStreams.ScavengeConfigurationStream, -1, 1, false, SystemAccounts.System,
				OnScavengeConfigurationRead);
		}


		public void Handle(SystemMessage.StateChangeMessage message) {
			if (message.State == VNodeState.Leader || message.State == VNodeState.Follower) {
				_logManager.Initialise();
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

		private void OnScavengeConfigurationRead(ClientMessage.ReadStreamEventsBackwardCompleted result) {
			if (result.Result is ReadStreamResult.Success or ReadStreamResult.NoStream) {
				if (result.Events.Length == 1) {
					_scavengeConfiguration = result.Events[0].OriginalEvent.Data.ParseJson<ScavengeConfiguration>();
				}

				var state = new PastScavengeState();
				_ioDispatcher.ReadForward(SystemStreams.ScavengesStream, 0, 500, true, SystemAccounts.System,
					res => OnReadingPastScavenges(state, res));

				return;
			}

			_ioDispatcher.ReadBackward(SystemStreams.ScavengeConfigurationStream, -1, 1, false, SystemAccounts.System,
				OnScavengeConfigurationRead);
		}

		private void OnReadingPastScavenges(PastScavengeState state, ClientMessage.ReadStreamEventsForwardCompleted result) {
			if (result.Result is ReadStreamResult.Success or ReadStreamResult.NoStream) {

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
							state.IncompleteScavenges.Add(scavengeId, @event.OriginalEvent.TimeStamp);
							break;

						case SystemEventTypes.ScavengeCompleted: {

							if (!dictionary.TryGetValue("timeTaken", out var timeTakenEntry))
								continue;

							if (!state.IncompleteScavenges.Remove(scavengeId, out var started))
								continue;

							var timeTaken = TimeSpan.Parse(timeTakenEntry.ToString());
							state.LastCompleteScavengeDate = started + timeTaken;
							break;
						}
					}
				}

				if (result.IsEndOfStream) {
					if (state.IncompleteScavenges.Count == 0 && state.LastCompleteScavengeDate.HasValue && _scavengeConfiguration != null) {
					}

					_initialized = true;
				} else {
					_from = result.NextEventNumber;
					_ioDispatcher.ReadForward(SystemStreams.ScavengesStream, _from, 500, true,
						SystemAccounts.System, res => OnReadingPastScavenges(state, res));
				}

				return;
			}

			_ioDispatcher.ReadForward(SystemStreams.ScavengesStream, _from, 500, true, SystemAccounts.System,
				res => OnReadingPastScavenges(state, res));
		}

		private struct PastScavengeState() {
			internal Dictionary<string, DateTime> IncompleteScavenges = new();
			internal DateTime? LastCompletedScavengeStarted;
			internal TimeSpan LastCompleteScavengeTimeTaken = TimeSpan.Zero;
		}
	}
}
