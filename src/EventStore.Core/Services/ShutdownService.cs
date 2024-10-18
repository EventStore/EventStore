// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using Serilog;

namespace EventStore.Core.Services;

public class ShutdownService :
	IHandle<SystemMessage.RegisterForGracefulTermination>,
	IHandle<SystemMessage.ComponentTerminated>,
	IHandle<ClientMessage.RequestShutdown> {

	private static readonly ILogger Log = Serilog.Log.ForContext<ShutdownService>();

	private readonly IPublisher _mainQueue;
	private readonly VNodeInfo _nodeInfo;
	private readonly List<Action> _shutdownActions = [];

	private int _componentsNeedingTermination;
	private bool _shutdown;
	private bool _exitProcess;
	private bool _shutdownHttp;

	public ShutdownService(IPublisher mainQueue, VNodeInfo nodeInfo) {
		_mainQueue = mainQueue;
		_nodeInfo = nodeInfo;
	}

	public void Handle(SystemMessage.RegisterForGracefulTermination message) {
		Log.Information("========== [{HttpEndPoint}] Component '{Component}' is registered for graceful termination", _nodeInfo.HttpEndPoint,
			message.ComponentName);

		_shutdownActions.Add(message.Action);
	}

	public void Handle(ClientMessage.RequestShutdown message) {
		if (_shutdown)
			return;

		_shutdown = true;
		_exitProcess = message.ExitProcess;
		_shutdownHttp = message.ShutdownHttp;
		_componentsNeedingTermination = _shutdownActions.Count;

		if (_componentsNeedingTermination == 0) {
			_mainQueue.Publish(new SystemMessage.BecomeShuttingDown(
				correlationId: Guid.NewGuid(),
				exitProcess: _exitProcess,
				shutdownHttp: _shutdownHttp));
			return;
		}

		Log.Information("========== [{httpEndPoint}] IS SHUTTING DOWN HIGHER LEVEL COMPONENTS...", _nodeInfo.HttpEndPoint);
		foreach (var action in _shutdownActions)
			action();
	}

	public void Handle(SystemMessage.ComponentTerminated message) {
		Log.Information("========== [{HttpEndPoint}] Component '{ComponentName}' has shut down.", _nodeInfo.HttpEndPoint,
			message.ComponentName);

		_componentsNeedingTermination -= 1;

		if (_componentsNeedingTermination == 0) {
			Log.Information("========== [{HttpEndPoint}] All Components Shutdown.", _nodeInfo.HttpEndPoint);
			_mainQueue.Publish(new SystemMessage.BecomeShuttingDown(
				correlationId: Guid.NewGuid(),
				exitProcess: _exitProcess,
				shutdownHttp: _shutdownHttp));
		}
	}
}
