// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services.PeriodicLogs;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.PeriodicLogs; 

public class PeriodicallyLoggingServiceTests {

	private FakePublisher _publisher;
	private FakeLogger _logger;
	private string _esVersion;

	[SetUp]
	public void SetUp() {
		_publisher = new();
		_logger = new FakeLogger();
		_esVersion = "0.0.0.0";
	}
	
	[Test]
	public void on_start() {
		// given
		var sut = new PeriodicallyLoggingService(_publisher, _esVersion, _logger);
		
		// when
		sut.Handle(new SystemMessage.SystemStart());
		
		// then
		Assert.IsInstanceOf<MonitoringMessage.CheckEsVersion>(_publisher.Messages.Single());
		Assert.IsEmpty(_logger.LogMessages);
	}

	[Test]
	public void log_es_version_periodically() {
		// given
		var sut = new PeriodicallyLoggingService(_publisher, _esVersion, _logger);
		
		// when
		sut.Handle(new MonitoringMessage.CheckEsVersion());
		
		// then
		var schedule = (TimerMessage.Schedule)_publisher.Messages.Single();
		Assert.AreEqual(TimeSpan.FromHours(12), schedule.TriggerAfter);
		Assert.IsInstanceOf<MonitoringMessage.CheckEsVersion>(schedule.ReplyMessage);

		var logMessage = _logger.LogMessages.Single();
		Assert.AreEqual($"Current version of Event Store is : \"0.0.0.0\" ", logMessage.RenderMessage());
	} 

}
