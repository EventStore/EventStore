using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Cluster;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Plugins.Subsystems;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EventStore.Loki;

public class ChaosPlugin : ISubsystemsPlugin, ISubsystem {
	private static readonly ILogger DefaultLogger = Log.ForContext<ChaosPlugin>();

	public string Name => "Loki";
	public string Version { get; } = typeof(ChaosPlugin).Assembly.GetName().Version!.ToString();
	public string CommandLineName => "loki";

	private readonly ISubsystem[] _subsystems;
	private readonly ILogger _logger;

	public IReadOnlyList<ISubsystem> GetSubsystems() => _subsystems;

	public IApplicationBuilder Configure(IApplicationBuilder builder) {
		var standardComponents = builder.ApplicationServices.GetRequiredService<StandardComponents>();

		var chaosMonkey = new ChaosMonkey(standardComponents.MainQueue, _logger);
		standardComponents.MainBus.Subscribe<SystemMessage.SystemInit>(chaosMonkey);
		standardComponents.MainBus.Subscribe<GossipMessage.GossipUpdated>(chaosMonkey);
		standardComponents.MainBus.Subscribe<ReplicationMessage.SubscribeToLeader>(chaosMonkey);

		return builder;
	}

	public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
		=> services;

	public Task Start() => Task.CompletedTask;

	public Task Stop() => Task.CompletedTask;

	public ChaosPlugin(ILogger? logger = null) {
		_subsystems = [this];
		_logger = logger ?? DefaultLogger;
	}

	private class ChaosMonkey(IPublisher mainQueue, ILogger logger) :
		IHandle<GossipMessage.GossipUpdated>, IHandle<SystemMessage.SystemInit>, IHandle<ReplicationMessage.SubscribeToLeader> {
		private ClusterInfo? _clusterInfo;
		private Guid _instanceId;
		private bool _becameClone;
		private Guid _subscriptionId;

		public void Handle(GossipMessage.GossipUpdated message) {
			if (!message.ClusterInfo.HasChangedSince(_clusterInfo)) {
				return;
			}

			_clusterInfo = message.ClusterInfo;

			if (_becameClone) {
				return;
			}

			// do we have a leader?
			var leader = message.ClusterInfo.Members.FirstOrDefault(m => m.State == VNodeState.Leader);

			if (leader is null) {
				return;
			}

			// are the rest of the nodes followers?
			if (message.ClusterInfo.Members.Count(member => member.State != VNodeState.Follower) ==
			    message.ClusterInfo.Members.Length - 1) {
				return;
			}

			var node = message.ClusterInfo.Members
				.OrderBy(x => x.InstanceId)
				.FirstOrDefault(m => m.State == VNodeState.Follower);

			if (node is null || node.InstanceId != _instanceId) {
				return;
			}

			mainQueue.Publish(TimerMessage.Schedule.Create(TimeSpan.FromSeconds(30), new PublishEnvelope(mainQueue),
				new ReplicationMessage.CloneAssignment(leader.InstanceId, _subscriptionId)));

			_becameClone = true;

			logger.Information("Node {nodeId} will transition to clone in 30 seconds.", _instanceId);
		}

		public void Handle(SystemMessage.SystemInit message) {
			logger.Information("NodeId is {nodeId}", message.InstanceId);
			_instanceId = message.InstanceId;
		}

		public void Handle(ReplicationMessage.SubscribeToLeader message) => _subscriptionId = message.SubscriptionId;
	}
}
