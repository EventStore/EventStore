﻿using System;
using System.Threading;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace EventStore.Projections.Core.Javascript.Tests.Integration
{
	public abstract class ProjectionRuntimeScenario: SubsystemScenario {
		static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder().AddInMemoryCollection().Build();

		protected ProjectionRuntimeScenario() : base(CreateRuntime, "$et", new CancellationTokenSource(System.Diagnostics.Debugger.IsAttached?5*60*1000: 5*1000).Token){

		}

		static (Action, IPublisher) CreateRuntime(InMemoryBus mainBus, IQueuedHandler mainQueue, ICheckpoint writerCheckpoint) {
			var options = new ProjectionSubsystemOptions(3, ProjectionType.All, true, TimeSpan.FromMinutes(5), false, 500, 500);
			var config = new TFChunkDbConfig("mem", new VersionedPatternFileNamingStrategy("mem", "chunk-"), 10000, 0, writerCheckpoint, new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), new InMemoryCheckpoint(-1), 1, 1, true);
			var db = new TFChunkDb(config);
			var qs = new QueueStatsManager();
			var timeProvider = new RealTimeProvider();
			var ts = new TimerService(new TimerBasedScheduler(new RealTimer(), timeProvider));
			var sc = new StandardComponents(db.Config, mainQueue, mainBus, ts, timeProvider, null, new IHttpService[] { }, mainBus, qs, new());

			var subsystem = new ProjectionsSubsystem(options);

			var builder = WebApplication.CreateBuilder();
			builder.Services.AddGrpc();
			builder.Services.AddSingleton(sc);
			subsystem.ConfigureServices(builder.Services, new ConfigurationBuilder().Build());

			subsystem.ConfigureApplication(builder.Build().UseRouting(), EmptyConfiguration);
			subsystem.Start();

			return (() => {
				subsystem.Stop();
				db.Dispose();
			}, subsystem.LeaderQueue);
		}
	}
}
