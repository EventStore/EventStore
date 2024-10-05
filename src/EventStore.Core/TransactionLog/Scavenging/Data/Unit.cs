// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Scavenging;

public struct Unit : IEquatable<Unit> {
	public static readonly Unit Instance = new Unit();

	public override int GetHashCode() => 1;
	public override bool Equals(object other) => other is Unit;
	public bool Equals(Unit other) => true;
}
