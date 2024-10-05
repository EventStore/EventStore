// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Scavenging;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class AdHocIndexScavengerInterceptor : IIndexScavenger {
	private readonly IIndexScavenger _wrapped;
	private readonly Func<Func<IndexEntry, bool>, Func<IndexEntry, bool>> _f;

	public AdHocIndexScavengerInterceptor(
		IIndexScavenger wrapped,
		Func<Func<IndexEntry, bool>, Func<IndexEntry, bool>> f) {

		_wrapped = wrapped;
		_f = f;
	}

	public void ScavengeIndex(
		long scavengePoint,
		Func<IndexEntry, bool> shouldKeep,
		IIndexScavengerLog log,
		CancellationToken cancellationToken) {

		_wrapped.ScavengeIndex(scavengePoint, _f(shouldKeep), log, cancellationToken);
	}
}
