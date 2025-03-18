// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

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
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using Serilog;
using static EventStore.Core.Messages.ClientMessage;

namespace EventStore.Core.Services.Storage;

// This tracks the current scavenge and starts/stops/creates it according to the client instructions
// It works for both old and new scavenge, determined by ScavengerFactory
public class StorageScavenger(ITFChunkScavengerLogManager logManager, ScavengerFactory scavengerFactory, SemaphoreSlimLock switchChunksLock) :
	IHandle<ScavengeDatabase>,
	IHandle<StopDatabaseScavenge>,
	IHandle<GetCurrentDatabaseScavenge>,
	IHandle<GetLastDatabaseScavenge>,
	IHandle<SystemMessage.StateChangeMessage> {
	protected static ILogger Log { get; } = Serilog.Log.ForContext<StorageScavenger>();
	private readonly ITFChunkScavengerLogManager _logManager = Ensure.NotNull(logManager);
	private readonly ScavengerFactory _scavengerFactory = Ensure.NotNull(scavengerFactory);
	private readonly SemaphoreSlimLock _switchChunksLock = Ensure.NotNull(switchChunksLock);
	private Guid _switchChunksLockId = Guid.Empty;
	private readonly object _lock = new object();

	private IScavenger _currentScavenge;

	// invariant: _currentScavenge is not null => _currentScavengeTask is the task of the current scavenge
	private Task<ScavengeResult> _currentScavengeTask;
	private CancellationTokenSource _cancellationTokenSource;

	private string _lastScavengeId;
	private LastScavengeResult _lastScavengeResult = LastScavengeResult.Unknown;

	public void Handle(SystemMessage.StateChangeMessage message) {
		if (message.State == VNodeState.Leader || message.State == VNodeState.Follower) {
			_logManager.Initialise();
		}
	}

	public void Handle(ScavengeDatabase message) {
		if (!IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
			return;
		}

		lock (_lock) {
			if (_currentScavenge != null) {
				message.Envelope.ReplyWith(new ScavengeDatabaseInProgressResponse(
					message.CorrelationId,
					_currentScavenge.ScavengeId,
					"Scavenge is already running"));
			} else if (!_switchChunksLock.TryAcquire(out _switchChunksLockId)) {
				Log.Information("SCAVENGING: Failed to acquire the chunks lock");
				message.Envelope.ReplyWith(new ScavengeDatabaseInProgressResponse(
					message.CorrelationId,
					Guid.Empty.ToString(),
					"Failed to acquire the chunk switch lock"));
			} else {
				Log.Information("SCAVENGING: Acquired the chunks lock");
				var tfChunkScavengerLog = _logManager.CreateLog();
				var logger = Log.ForContext("ScavengeId", tfChunkScavengerLog.ScavengeId);

				_cancellationTokenSource = new();

				_currentScavenge = _scavengerFactory.Create(message, tfChunkScavengerLog, logger);
				_currentScavengeTask = _currentScavenge.ScavengeAsync(_cancellationTokenSource.Token);
				_lastScavengeId = _currentScavenge.ScavengeId;
				_lastScavengeResult = LastScavengeResult.InProgress;

				HandleCleanupWhenFinished(_currentScavengeTask, _currentScavenge, logger);

				message.Envelope.ReplyWith(new ScavengeDatabaseStartedResponse(message.CorrelationId, tfChunkScavengerLog.ScavengeId));
			}
		}
	}

	public void Handle(StopDatabaseScavenge message) {
		if (!IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
			return;
		}

		lock (_lock) {
			if (_currentScavenge != null &&
			    (_currentScavenge.ScavengeId == message.ScavengeId || message.ScavengeId == "current")) {
				_cancellationTokenSource.Cancel();

				_currentScavengeTask.ContinueWith(_ => {
					message.Envelope.ReplyWith(new ScavengeDatabaseStoppedResponse(message.CorrelationId, _currentScavenge.ScavengeId));
				});
			} else {
				message.Envelope.ReplyWith(new ScavengeDatabaseNotFoundResponse(message.CorrelationId, _currentScavenge?.ScavengeId, "Scavenge Id does not exist"));
			}
		}
	}

	public void Handle(GetCurrentDatabaseScavenge message) {
		if (!IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
			return;
		}

		lock (_lock) {
			if (_currentScavenge != null) {
				message.Envelope.ReplyWith(new ScavengeDatabaseGetCurrentResponse(
					message.CorrelationId,
					ScavengeDatabaseGetCurrentResponse.ScavengeResult.InProgress,
					_currentScavenge.ScavengeId));
			} else {
				message.Envelope.ReplyWith(new ScavengeDatabaseGetCurrentResponse(
					message.CorrelationId, ScavengeDatabaseGetCurrentResponse.ScavengeResult.Stopped, scavengeId: null));
			}
		}
	}

	public void Handle(GetLastDatabaseScavenge message) {
		if (!IsAllowed(message.User, message.CorrelationId, message.Envelope)) {
			return;
		}

		lock (_lock) {
			var response = new ScavengeDatabaseGetLastResponse(
				message.CorrelationId,
				_lastScavengeResult switch {
					LastScavengeResult.Unknown => ScavengeDatabaseGetLastResponse.ScavengeResult.Unknown,
					LastScavengeResult.Success => ScavengeDatabaseGetLastResponse.ScavengeResult.Success,
					LastScavengeResult.Errored => ScavengeDatabaseGetLastResponse.ScavengeResult.Errored,
					LastScavengeResult.Stopped => ScavengeDatabaseGetLastResponse.ScavengeResult.Stopped,
					LastScavengeResult.InProgress => ScavengeDatabaseGetLastResponse.ScavengeResult.InProgress,
					_ => throw new ArgumentOutOfRangeException(nameof(_lastScavengeResult))
				},
				_lastScavengeId);

			message.Envelope.ReplyWith(response);
		}
	}

	private async void HandleCleanupWhenFinished(Task<ScavengeResult> newScavengeTask, IScavenger newScavenge, ILogger logger) {
		// Clean up the reference to the TfChunkScavenger once it's finished.
		try {
			var result = await newScavengeTask;

			lock (_lock) {
				_lastScavengeResult = result switch {
					ScavengeResult.Success => LastScavengeResult.Success,
					ScavengeResult.Errored => LastScavengeResult.Errored,
					ScavengeResult.Stopped => LastScavengeResult.Stopped,
					_ => throw new ArgumentOutOfRangeException(nameof(result))
				};
			}
		} catch (Exception ex) {
			logger.Error(ex, "SCAVENGING: Unexpected error when scavenging");

			lock (_lock) {
				_lastScavengeResult = LastScavengeResult.Errored;
			}
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
			logger.Information(_switchChunksLock.TryRelease(switchChunksLockId) ? "SCAVENGING: Released the chunks lock" : "SCAVENGING: Failed to release the chunks lock");
		} catch (Exception ex) {
			logger.Error(ex, "SCAVENGING: Unexpected error when releasing the chunks lock");
		}

		lock (_lock) {
			if (newScavenge == _currentScavenge) {
				_currentScavenge = null;
			}
		}
	}

	private static bool IsAllowed(ClaimsPrincipal user, Guid correlationId, IEnvelope envelope) {
		if (user != null && (user.LegacyRoleCheck(SystemRoles.Admins) || user.LegacyRoleCheck(SystemRoles.Operations))) {
			return true;
		}

		envelope.ReplyWith(new ScavengeDatabaseUnauthorizedResponse(correlationId, null, "User not authorized"));
		return false;
	}
}
