// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using System.Threading;

namespace EventStore.POC.ConnectorsEngine.Infrastructure;

public interface IRepository {
	Task<(T, long)> LoadAsync<T>(
		string id,
		CancellationToken ct) where T : Aggregate;

	Task SaveAsync(
		string name,
		Aggregate x,
		long expectedVersion,
		CancellationToken ct);
}
