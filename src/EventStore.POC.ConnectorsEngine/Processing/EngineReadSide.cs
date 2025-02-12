// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.ConnectorsEngine.Processing.Activation;
using EventStore.POC.ConnectorsEngine.Processing.Checkpointing;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing;

//qq consider name
public sealed class EngineReadSide : IDisposable {
	private static readonly ILogger _logger = Log.ForContext<EngineReadSide>();

	private readonly IClient _client;
	private readonly INamingStrategy _namingStrategy;
	private readonly ConnectorActivator _activator;
	private readonly Channel<Message> _channel;
	private readonly ISerializer _serializer;
	private readonly CancellationTokenSource _cts = new();

	public EngineReadSide(
		IClient client,
		INamingStrategy namingStrategy,
		ISerializer serializer) {

		_activator = new ConnectorActivator(
			activationPolicy: new DistributingActivationPolicy(),
			connectorFactory: ConnectorFactory);

		_channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(64) {
			FullMode = BoundedChannelFullMode.Wait,
		});

		_client = client;
		_namingStrategy = namingStrategy;
		_serializer = serializer;
	}

	public Task Start() {
		MonitorConnectors(_channel.Writer, _cts.Token).WarnOnCompletion("CONNECTORS MONITOR STOPPED");
		MonitorGossipChanges(_channel.Writer, _cts.Token).WarnOnCompletion("GOSSIP MONITOR STOPPED");
		PumpToActivator(_channel, _cts.Token).WarnOnCompletion("PUMP TO ACTIVATOR STOPPED");
		return Task.CompletedTask;
	}

	public void Dispose() {
		_cts.Cancel();
	}

	private async Task MonitorGossipChanges(
		ChannelWriter<Message> channelWriter,
		CancellationToken ct) {

		//qq probably want some kind of autoresubscribe....extension method? and other places
		await _client
			.SubscribeToStream("$mem-gossip", ct)
			.DeserializeSkippingUnknown(_serializer)
			.ToChannelAsync(channelWriter, ct);
	}

	private IAsyncEnumerable<Message> ReadEntireStream(string stream, CancellationToken ct) => _client
		.ReadStreamForwards(stream, int.MaxValue, ct)
		.HandleStreamNotFound()
		.DeserializeSkippingUnknown(_serializer);

	private async Task MonitorConnectors(ChannelWriter<Message> channelWriter, CancellationToken ct) {
		//qq this is a bit of a pain.
		// this wants to start a live subscription to $all, and probably do a reverse read1 to see what the latest event is
		// then it wants to read the registration stream to see what connectors it should read
		// then it should read all the connectors (but ignoring events after the subscription started)
		// then it should apply the events that the subscription has received.
		// it would be much easier if we just had a stream of all connector events.
		// for now this is a naive implementation that might miss events in the transition to live.

		HashSet<string> connectorIds = new();
		await foreach (var evt in ReadEntireStream(ConnectorConsts.ConnectorRegistrationStream, ct)) {
			if (evt is ContextMessage<Events.ConnectorRegistered> registered) {
				connectorIds.Add(registered.Payload.ConnectorId);
			} else if (evt is ContextMessage<Events.ConnectorDeregistered> deregistered) {
				connectorIds.Remove(deregistered.Payload.ConnectorId);
			}
		}

		foreach (var connectorId in connectorIds) {
			await ReadEntireStream(_namingStrategy.NameFor(connectorId), ct)
				.ToChannelAsync(channelWriter, ct);
		}

		await channelWriter.WriteAsync(new Messages.CaughtUp(), ct);

		await _client
			.SubscribeToAll(FromAll.End, ct)
			.Where(x => x.EventType.StartsWith("$Connector")) //qq todo: serverside filter
			.DeserializeSkippingUnknown(_serializer)
			.ToChannelAsync(channelWriter, ct);
	}

	private async Task PumpToActivator(Channel<Message> channel, CancellationToken ct) {
		try {
			await channel.Reader
				.ReadAllAsync(ct)
				.ForEachAsync(_activator.Handle, ct);
		} catch (Exception ex) {
			channel.Writer.TryComplete(ex);
		}
	}

	//qq this is out here for the moment because it uses the client.
	private Connector ConnectorFactory(ConnectorState state) {
		var sinkConfig = new UriBuilder(state.Sink).Uri;
		IFilter filter = IFilter.None.Instance;

		if (!string.IsNullOrEmpty(state.Filter)) {
			filter = new JsonPathFilter(state.Filter, _logger);
		}

		var sink = SinkFactory.Create(state.Id, sinkConfig, _logger);
		_logger.Information("Creating Connector: id: {id}, filter: {filter}, sink:{sink}",
			state.Id, state.Filter ?? "<none>", sinkConfig.Scheme);

		var checkpointManager = new ConnectorCheckpointManager(
			state.Id,
			state.CheckpointConfig ?? CheckpointConfig.None,
			state.ResetTo,
			new CheckpointNamingStrategy(_namingStrategy),
			_client,
			_serializer,
			_logger);

		var connector = new Connector(state.Id, filter, _client, sink, _logger, checkpointManager);
		return connector;
	}

	public async Task<ConnectorState[]> ListAsync() {
		//qq todo: consider when not caught up or when disposed
		var tcs = new TaskCompletionSource<ConnectorState[]>(TaskCreationOptions.RunContinuationsAsynchronously);
		await _channel.Writer.WriteAsync(new Messages.GetConnectorList(xs => tcs.TrySetResult(xs)));
		return await tcs.Task;
	}

	public async Task<string[]> ListActiveConnectorsAsync() {
		var tcs = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
		await _channel.Writer.WriteAsync(new Messages.GetActiveConnectorsList(xs => tcs.TrySetResult(xs)));
		return await tcs.Task;
	}
}

public static class Extensions {
	public static Task WarnOnCompletion(this Task task, string warning) {
		return task.ContinueWith(t => {
			if (t.Exception is null)
				return;
			Log.Warning(t.Exception, warning);
		});
	}
}
