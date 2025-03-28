// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using Serilog;

namespace EventStore.Core.Services.PeriodicLogs;

public class PeriodicallyLoggingService :
	IHandle<SystemMessage.SystemStart>,
	IHandle<MonitoringMessage.CheckEsVersion> {

	private static readonly TimeSpan _interval = TimeSpan.FromHours(12);

	private readonly IPublisher _publisher;
	private readonly string _esVersion;
	private readonly ILogger _logger;
	private readonly TimerMessage.Schedule _esVersionScheduleLog;

	public PeriodicallyLoggingService(IPublisher publisher, string esVersion, ILogger logger) {
		Ensure.NotNull(publisher, nameof(publisher));
		Ensure.NotNull(logger, nameof(logger));

		_publisher = publisher;
		_esVersion = esVersion;
		_logger = logger;
		_esVersionScheduleLog = TimerMessage.Schedule.Create(_interval, publisher,
			new MonitoringMessage.CheckEsVersion());
	}

	public void Handle(SystemMessage.SystemStart message) {
		_publisher.Publish(new MonitoringMessage.CheckEsVersion());
	}

	public void Handle(MonitoringMessage.CheckEsVersion message) {
		_logger.Information("Current version of KurrentDB is : {dbVersion} ", _esVersion);
		_publisher.Publish(_esVersionScheduleLog);
	}

}
