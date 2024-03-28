﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Projections.Core.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Subsystem {
	public class TestFixtureWithProjectionSubsystem {
		private StandardComponents _standardComponents;

		protected ProjectionsSubsystem Subsystem;
		protected const int WaitTimeoutMs = 3000;

		private readonly ManualResetEvent _stopReceived = new ManualResetEvent(false);
		private ProjectionSubsystemMessage.StopComponents _lastStopMessage;

		private readonly ManualResetEvent _startReceived = new ManualResetEvent(false);
		private ProjectionSubsystemMessage.StartComponents _lastStartMessage;

		protected Task Started { get; private set; }

		private StandardComponents CreateStandardComponents() {
			var dbConfig = TFChunkHelper.CreateDbConfig(Path.GetTempPath(), 0);
			var mainQueue = QueuedHandler.CreateQueuedHandler
			(new AdHocHandler<Message>(msg => {
				/* Ignore messages */
			}), "MainQueue", new QueueStatsManager(), new());
			var mainBus = new InMemoryBus("mainBus");
			var threadBasedScheduler = new ThreadBasedScheduler(new QueueStatsManager(), new());
			var timerService = new TimerService(threadBasedScheduler);

			return new StandardComponents(dbConfig, mainQueue, mainBus,
				timerService, timeProvider: null, httpForwarder: null, httpServices: new IHttpService[] { },
				networkSendService: null, queueStatsManager: new QueueStatsManager(),
				trackers: new());
		}

		[OneTimeSetUp]
		public void SetUp() {
			_standardComponents = CreateStandardComponents();

			var builder = WebApplication.CreateBuilder();
			builder.Services.AddGrpc();
			builder.Services.AddSingleton(_standardComponents);

			Subsystem = new ProjectionsSubsystem(
				new ProjectionSubsystemOptions(1, ProjectionType.All, true, TimeSpan.FromSeconds(3), true, 500, 250));

			Subsystem.ConfigureServices(builder.Services, new ConfigurationBuilder().Build());
			Subsystem.Configure(builder.Build().UseRouting());

			// Unsubscribe from the actual components so we can test in isolation
			Subsystem.LeaderMainBus.Unsubscribe<ProjectionSubsystemMessage.ComponentStarted>(Subsystem);
			Subsystem.LeaderMainBus.Unsubscribe<ProjectionSubsystemMessage.ComponentStopped>(Subsystem);

			Subsystem.LeaderMainBus.Subscribe(new AdHocHandler<Message>(
				msg => {
					switch (msg) {
						case ProjectionSubsystemMessage.StartComponents start: {
							_lastStartMessage = start;
							_startReceived.Set();
							break;
						}
						case ProjectionSubsystemMessage.StopComponents stop: {
							_lastStopMessage = stop;
							_stopReceived.Set();
							break;
						}
					}
				}));

			Started = Subsystem.Start();

			Given();
		}

		[OneTimeTearDown]
		public void TearDown() {
			_standardComponents.TimerService.Dispose();
		}

		protected virtual void Given() {
		}

		protected ProjectionSubsystemMessage.StartComponents WaitForStartMessage
			(string timeoutMsg = null, bool failOnTimeout = true) {
			timeoutMsg ??= "Timed out waiting for Start Components";
			if (_startReceived.WaitOne(WaitTimeoutMs))
				return _lastStartMessage;
			if (failOnTimeout)
				Assert.Fail(timeoutMsg);
			return null;
		}

		protected ProjectionSubsystemMessage.StopComponents WaitForStopMessage(string timeoutMsg = null) {
			timeoutMsg ??= "Timed out waiting for Stop Components";
			if (_stopReceived.WaitOne(WaitTimeoutMs)) {
				return _lastStopMessage;
			}

			Assert.Fail(timeoutMsg);
			return null;
		}

		protected void ResetMessageEvents() {
			_stopReceived.Reset();
			_startReceived.Reset();
			_lastStopMessage = null;
			_lastStartMessage = null;
		}
	}
}
