// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.ConnectorsEngine.Processing;
using EventStore.POC.ConnectorsEngine.Management;
using EventStore.POC.IO.Core;
using Serilog;
using EventStore.POC.IO.Core.Serialization;

namespace EventStore.POC.ConnectorsEngine;

public static class ConnectorConsts {
	public const string ConnectorRegistrationStream = "$connector-preview-registration";
	public const string ConnectorDefinitionStreamCategory = "$connector-preview-";
}

public class Engine {
	private static readonly ILogger _logger = Serilog.Log.ForContext<Engine>();

	private readonly IClient _client;
	private readonly INamingStrategy _namingStrategy;
	private readonly ISerializer _serializer;

	private volatile bool _started = false;
	private EngineReadSide? _readSide;
	private readonly ConnectorsService _connectorsService;

	public EngineReadSide ReadSide => _readSide ?? throw new InvalidOperationException("Not started");

	public ConnectorsService WriteSide => _connectorsService;

	public Engine(IClient client) {
		_client = client;

		_namingStrategy = new ConnectorNamingStrategy();

		var options = new JsonSerializerOptions() {
			PropertyNameCaseInsensitive = true,
			Converters = {
				new EnumConverterWithDefault<NodeState>(),
			}
		};

		ISerializer serializer = new SerializerBuilder()
			.SerializeJson(options, b => b
				.ReadSystemEventWithContext<Messages.GossipUpdated>(version: null)
				.ReadSystemEventWithContext<Events.ConnectorDeregistered>()
				.ReadSystemEventWithContext<Events.ConnectorRegistered>()
				.ReadSystemEventWithContext<Events.ConnectorCreated>(_namingStrategy)
				.ReadSystemEventWithContext<Events.ConnectorEnabled>(_namingStrategy)
				.ReadSystemEventWithContext<Events.ConnectorDisabled>(_namingStrategy)
				.ReadSystemEventWithContext<Events.ConnectorReset>(_namingStrategy)
				.ReadSystemEventWithContext<Events.ConnectorDeleted>(_namingStrategy)
				.RegisterSystemEvent<Messages.AllCheckpointed>())
			.Build();
		_serializer = new LoggingSerializer(serializer, _logger);

		ISerializer writeSideSerializer = new SerializerBuilder()
			.SerializeJson(options, b => b
				.RegisterSystemEvent<Events.ConnectorRegistered>()
				.RegisterSystemEvent<Events.ConnectorDeregistered>()
				.RegisterSystemEvent<Events.ConnectorCreated>()
				.RegisterSystemEvent<Events.ConnectorEnabled>()
				.RegisterSystemEvent<Events.ConnectorDisabled>()
				.RegisterSystemEvent<Events.ConnectorReset>()
				.RegisterSystemEvent<Events.ConnectorDeleted>())
			.Build();
		writeSideSerializer = new LoggingSerializer(writeSideSerializer, _logger);

		_connectorsService = new(
			client: _client,
			serializer: writeSideSerializer,
			repository: new Repository(
				client: _client,
				namingStrategy: _namingStrategy,
				serializer: writeSideSerializer),
			namingStrategy: _namingStrategy);
	}

	public Task Start() {
		if (_started)
			throw new InvalidOperationException("Already started");

		_readSide = new(_client, _namingStrategy , _serializer);
		_started = true;

		return _readSide.Start();
	}

	public Task Stop() {
		if (_started) {
			_started = false;
			_readSide?.Dispose();
			_readSide = null;
		}

		return Task.CompletedTask;
	}
}
