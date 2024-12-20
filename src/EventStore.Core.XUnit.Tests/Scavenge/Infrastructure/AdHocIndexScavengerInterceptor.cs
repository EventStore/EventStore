// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;

public class AdHocIndexScavengerInterceptor : IIndexScavenger {
	private readonly IIndexScavenger _wrapped;
	private readonly Func<Func<IndexEntry, CancellationToken, ValueTask<bool>>, Func<IndexEntry, CancellationToken, ValueTask<bool>>> _f;

	public AdHocIndexScavengerInterceptor(
		IIndexScavenger wrapped,
		Func<Func<IndexEntry, CancellationToken, ValueTask<bool>>, Func<IndexEntry, CancellationToken, ValueTask<bool>>> f) {

		_wrapped = wrapped;
		_f = f;
	}

	public ValueTask ScavengeIndex(
		long scavengePoint,
		Func<IndexEntry, CancellationToken, ValueTask<bool>> shouldKeep,
		IIndexScavengerLog log,
		CancellationToken cancellationToken) {

		return _wrapped.ScavengeIndex(scavengePoint,_f(shouldKeep), log, cancellationToken);
	}
}
