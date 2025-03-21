// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using Serilog;

namespace EventStore.Core.Services.PeriodicLogs;

public class PeriodicallyLoggingService(IPublisher publisher, string esVersion, ILogger logger) :
	IHandle<SystemMessage.SystemStart>,
	IHandle<MonitoringMessage.CheckEsVersion> {
	private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

	private readonly IPublisher _publisher = Ensure.NotNull(publisher);
	private readonly ILogger _logger = Ensure.NotNull(logger);
	private readonly TimerMessage.Schedule _esVersionScheduleLog = TimerMessage.Schedule.Create(Interval, publisher, new MonitoringMessage.CheckEsVersion());

	public void Handle(SystemMessage.SystemStart message) {
		_publisher.Publish(new MonitoringMessage.CheckEsVersion());
	}

	public void Handle(MonitoringMessage.CheckEsVersion message) {
		_logger.Information("Current version of KurrentDB is : {dbVersion} ", esVersion);
		_publisher.Publish(_esVersionScheduleLog);
	}
}
