// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Metrics;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core;

public class StandardComponents(
	TFChunkDbConfig dbConfig,
	IPublisher mainQueue,
	ISubscriber mainBus,
	TimerService timerService,
	ITimeProvider timeProvider,
	IHttpForwarder httpForwarder,
	IUriRouter router,
	IPublisher networkSendService,
	QueueStatsManager queueStatsManager,
	QueueTrackers trackers,
	bool projectionStats) {

	public TFChunkDbConfig DbConfig { get; } = dbConfig;
	public IPublisher MainQueue { get; } = mainQueue;
	public ISubscriber MainBus { get; } = mainBus;
	public TimerService TimerService { get; } = timerService;
	public ITimeProvider TimeProvider { get; } = timeProvider;
	public IHttpForwarder HttpForwarder { get; } = httpForwarder;
	public IUriRouter Router { get; } = router;

	public IPublisher NetworkSendService { get; } = networkSendService;
	public QueueStatsManager QueueStatsManager { get; } = queueStatsManager;
	public bool ProjectionStats { get; } = projectionStats;
	public QueueTrackers QueueTrackers { get; private set; } = trackers;
}
