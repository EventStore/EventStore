// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public class Repository : IRepository {
	private readonly IClient _client;
	private readonly INamingStrategy _namingStrategy;
	private readonly ISerializer _serializer;

	public Repository(
		IClient client,
		INamingStrategy namingStrategy,
		ISerializer serializer) {

		_client = client;
		_namingStrategy = namingStrategy;
		_serializer = serializer;
	}

	public async Task<(T, long)> LoadAsync<T>(
		string id,
		CancellationToken ct)
		where T : Aggregate {

		var instance = Activator.CreateInstance<T>();
		instance.BaseState.Id = id;
		var stream = _namingStrategy.NameFor(id);
		var version = -1L;

		await foreach (var evt in _client
			.ReadStreamForwards(stream, int.MaxValue, ct)
			.HandleStreamNotFound()) {

			if (!_serializer.TryDeserialize(evt, out var msg, out var ex))
				throw ex!;

			instance.BaseState.Apply(msg!);
			version = (long)evt.EventNumber;
		}

		return (instance, version);
	}

	public async Task SaveAsync(
		string id,
		Aggregate x,
		long expectedVersion,
		CancellationToken ct) {

		if (x.BaseState.Pending.Count == 0)
			return;

		var stream = _namingStrategy.NameFor(id);
		var events = x.BaseState.Pending
			.Select(msg => _serializer.Serialize(msg))
			.ToArray();
		await _client.WriteAsync(stream, events, expectedVersion, ct);
	}
}
