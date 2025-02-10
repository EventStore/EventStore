// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Management;

public class ConnectorsService {
	private static readonly ILogger _logger = Log.ForContext<ConnectorsService>();

	private readonly IClient _client;
	private readonly ISerializer _serializer;
	private readonly IRepository _repository;
	private readonly INamingStrategy _namingStrategy;
	private readonly INamingStrategy _checkpointNamingStrategy;

	public ConnectorsService(
		IClient client,
		ISerializer serializer,
		IRepository repository,
		INamingStrategy namingStrategy) {

		_client = client;
		_serializer = serializer;
		_repository = repository;
		_namingStrategy = namingStrategy;
		_checkpointNamingStrategy = new CheckpointNamingStrategy(_namingStrategy);
	}

	public async Task Create(Commands.CreateConnector cmd, CancellationToken ct) {
		var (connector, version) = await _repository.LoadAsync<ConnectorManager>(cmd.ConnectorId, ct);
		connector.Create(
			connectorId: cmd.ConnectorId,
			filter: cmd.Filter,
			sink: cmd.Sink,
			affinity: cmd.Affinity,
			enable: cmd.Enable,
			checkpointInterval: cmd.checkpointInterval);

		// the registration stream is a hint to the read side of where to look for connectors
		// it is not the source of truth. it is ok if we write the hint and then do not create
		// a connector.
		await Register(cmd.ConnectorId, ct);

		await _repository.SaveAsync(cmd.ConnectorId, connector, version, ct);
	}

	public async Task Enable(string id, CancellationToken ct) {
		var (connector, version) = await _repository.LoadAsync<ConnectorManager>(id, ct);
		connector.Enable();
		await _repository.SaveAsync(id, connector, version, ct);
	}

	public async Task Disable(string id, CancellationToken ct) {
		var (connector, version) = await _repository.LoadAsync<ConnectorManager>(id, ct);
		connector.Disable();
		await _repository.SaveAsync(id, connector, version, ct);
	}

	public async Task Reset(Commands.ResetConnector cmd, CancellationToken ct) {
		var (connector, version) = await _repository.LoadAsync<ConnectorManager>(cmd.ConnectorId, ct);
		await connector.Reset(cmd.CommitPosition, cmd.PreparePosition, _client, ct);
		await _repository.SaveAsync(cmd.ConnectorId, connector, version, ct);
	}

	public async Task Delete(string id, CancellationToken ct) {
		var (connector, version) = await _repository.LoadAsync<ConnectorManager>(id, ct);
		connector.Delete();
		var newEventCount = connector.State.Pending.Count;
		await _repository.SaveAsync(id, connector, version, ct);

		// delete the connector stream at specific version. if the connector has been recreated
		// by someoen else then we do not delete it.
		await TryDeleteStream(_namingStrategy.NameFor(id), version + newEventCount, ct);

		// we don't have an expected version for the checkpoint stream, just delete it.
		// if the connector has been recreated and it has checkpointed then that checkpoint
		// could be deleted here but worst case that just means some events will be processed again.
		await TryDeleteStream(_checkpointNamingStrategy.NameFor(id), ExpectedVersion.Any, ct);

		// the registration stream is a hint to the read side of where to look for connectors
		// it is not the source of truth. it is ok if we delete the connector and then
		// do not deregister it.
		// todo: when we process registrations on startup if we find incomplete deregistrations we can
		// finish them off.
		await Deregister(id, ct);
	}

	private async Task TryDeleteStream(string stream, long expectedVersion, CancellationToken ct) {
		if (expectedVersion == ExpectedVersion.NoStream) {
			// deleting the stream when we expect no stream cannot do anything
			return;
		}

		try {
			await _client.DeleteStreamAsync(stream, expectedVersion, ct);
		} catch (ResponseException.WrongExpectedVersion) {
			// curiously, we get this if the stream does not exist.
		}
	}

	private async Task Register(string id, CancellationToken ct) {
		await _client.WriteAsync(
			ConnectorConsts.ConnectorRegistrationStream,
			new[] { _serializer.Serialize(new Events.ConnectorRegistered(id)) },
			-2,
			ct);
	}

	private async Task Deregister(string id, CancellationToken ct) {
		await _client.WriteAsync(
			ConnectorConsts.ConnectorRegistrationStream,
			new[] { _serializer.Serialize(new Events.ConnectorDeregistered(id)) },
			-2,
			ct);
	}
}
