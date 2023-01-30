using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Synchronization;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;
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
		private readonly object _lock = new object();

		private IScavenger _currentScavenge;
		// invariant: _currentScavenge is not null => _currentScavengeTask is the task of the current scavenge
		private Task _currentScavengeTask;
		private CancellationTokenSource _cancellationTokenSource;
		private IExclusiveLock _switchChunksLock;

		public StorageScavenger(
			ITFChunkScavengerLogManager logManager,
			ScavengerFactory scavengerFactory,
			IExclusiveLock switchChunksLock) {

			Ensure.NotNull(logManager, nameof(logManager));
			Ensure.NotNull(scavengerFactory, nameof(scavengerFactory));
			Ensure.NotNull(switchChunksLock, nameof(switchChunksLock));

			_logManager = logManager;
			_scavengerFactory = scavengerFactory;
			_switchChunksLock = switchChunksLock;
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
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress,
							_currentScavenge.ScavengeId));
					} else if (!_switchChunksLock.TryAcquire()) {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress,
							Guid.Empty.ToString()));
					} else {
						var tfChunkScavengerLog = _logManager.CreateLog();
						var logger = Log.ForContext("ScavengeId", tfChunkScavengerLog.ScavengeId);

						_cancellationTokenSource = new CancellationTokenSource();

						_currentScavenge = _scavengerFactory.Create(message, tfChunkScavengerLog, logger);
						_currentScavengeTask = _currentScavenge.ScavengeAsync(_cancellationTokenSource.Token);

						HandleCleanupWhenFinished(_currentScavengeTask, _currentScavenge, logger);

						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Started,
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
							message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
								ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Stopped,
								_currentScavenge.ScavengeId));
						});
					} else {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InvalidScavengeId,
							_currentScavenge?.ScavengeId));
					}
				}
			}
		}

		public void Handle(ClientMessage.GetDatabaseScavenge message) {
			if (IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
				lock (_lock) {
					if (_currentScavenge != null) {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(
							message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InProgress,
							_currentScavenge.ScavengeId));
					} else {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(
							message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Stopped,
							scavengeId: null));
					}
				}
			}
		}

		private async void HandleCleanupWhenFinished(Task newScavengeTask, IScavenger newScavenge, ILogger logger) {
			// Clean up the reference to the TfChunkScavenger once it's finished.
			try {
				await newScavengeTask.ConfigureAwait(false);
			} catch (Exception ex) {
				logger.Error(ex, "SCAVENGING: Unexpected error when scavenging");
			} finally {
				try {
					newScavenge.Dispose();
				} catch (Exception ex) {
					logger.Error(ex, "SCAVENGING: Unexpected error when disposing the scavenger");
				}
			}

			try {
				_switchChunksLock.TryRelease();
			} catch (Exception ex) {
				logger.Error(ex, "SCAVENGING: Unexpected error when releasing the chunks switch semaphore.");
			}

			lock (_lock) {
				if (newScavenge == _currentScavenge) {
					_currentScavenge = null;
				}
			}
		}

		private bool IsAllowed(ClaimsPrincipal user, Guid correlationId, IEnvelope envelope) {
			if (user == null || (!user.LegacyRoleCheck(SystemRoles.Admins) && !user.LegacyRoleCheck(SystemRoles.Operations))) {
				envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(correlationId,
					ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Unauthorized, null));
				return false;
			}

			return true;
		}
	}
}
