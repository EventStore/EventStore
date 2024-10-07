// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Scavenging;

// These are stored in the data of the payload record
public class ScavengePointPayload {
	public int Threshold { get; set; }

	public byte[] ToJsonBytes() =>
		Json.ToJsonBytes(this);

	public static ScavengePointPayload FromBytes(ReadOnlyMemory<byte> bytes) =>
		Json.ParseJson<ScavengePointPayload>(bytes);
}
