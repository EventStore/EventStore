// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.Threading;
using ILogger = Serilog.ILogger;

namespace EventStore.Core;

public class ClusterNodeMutex {
	private static readonly ILogger Log = Serilog.Log.ForContext<ClusterNodeMutex>();

	public readonly string MutexName = $"ESCLUSTERNODE:{Process.GetCurrentProcess().Id}";

	public bool IsAcquired {
		get { return _acquired; }
	}

	private Mutex _clusterNodeMutex;
	private bool _acquired;

	public bool Acquire() {
		if (_acquired)
			throw new InvalidOperationException($"Cluster Node mutex '{MutexName}' is already acquired.");
		try {
			_clusterNodeMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out _acquired);
		} catch (AbandonedMutexException exc) {
			Log.Information(exc, "Cluster Node mutex '{mutex}' is said to be abandoned. Probably previous instance of server was terminated abruptly.", MutexName);
		}

		return _acquired;
	}

	public void Release() {
		if (!_acquired)
			throw new InvalidOperationException($"Cluster Node mutex '{MutexName}' was not acquired.");
		_clusterNodeMutex.ReleaseMutex();
	}

	public static bool IsPresent(int pid) {
		var mutexName = $"ESCLUSTERNODE:{pid}";
		try {
			using (Mutex.OpenExisting(mutexName)) {
				return true;
			}
		} catch (WaitHandleCannotBeOpenedException) {
			return false;
		} catch (Exception exc) {
			Log.Debug(exc, "Exception while trying to open Cluster Node mutex '{mutex}': {e}.", mutexName,
				exc.Message);
		}

		return false;
	}
}
