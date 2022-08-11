using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.Services.Storage {
	// This tracks the current scavenge and starts/stops/creates it according to the client instructions
	public class StorageScavenger :
		IHandle<ClientMessage.ScavengeDatabase>,
		IHandle<ClientMessage.StopDatabaseScavenge>,
		IHandle<SystemMessage.StateChangeMessage> {

		private readonly ITFChunkScavengerLogManager _logManager;
		private readonly ScavengerFactory _scavengerFactory;
		private readonly object _lock = new object();

		private IScavenger _currentScavenge;
		private CancellationTokenSource _cancellationTokenSource;

		public StorageScavenger(
			ITFChunkScavengerLogManager logManager,
			ScavengerFactory scavengerFactory) {

			Ensure.NotNull(logManager, "logManager");
			Ensure.NotNull(scavengerFactory, "scavengerFactory");

			_logManager = logManager;
			_scavengerFactory = scavengerFactory;
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
					} else {
						var tfChunkScavengerLog = _logManager.CreateLog();

						_cancellationTokenSource = new CancellationTokenSource();

						var newScavenge = _currentScavenge = _scavengerFactory.Create(message, tfChunkScavengerLog);
						var newScavengeTask = _currentScavenge.ScavengeAsync(_cancellationTokenSource.Token);

						HandleCleanupWhenFinished(newScavengeTask, newScavenge);

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
					if (_currentScavenge != null && _currentScavenge.ScavengeId == message.ScavengeId) {
						_cancellationTokenSource.Cancel();

						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.Stopped,
							message.ScavengeId));
					} else {
						message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
							ClientMessage.ScavengeDatabaseResponse.ScavengeResult.InvalidScavengeId,
							_currentScavenge?.ScavengeId));
					}
				}
			}
		}

		private async void HandleCleanupWhenFinished(Task newScavengeTask, IScavenger newScavenge) {
			// Clean up the reference to the TfChunkScavenger once it's finished.
			try {
				await newScavengeTask.ConfigureAwait(false);
			} finally {
				newScavenge.Dispose();
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
