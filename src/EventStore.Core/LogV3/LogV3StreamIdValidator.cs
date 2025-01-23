// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3;

public class LogV3StreamIdValidator : IValidator<StreamId> {
	// Corresponding to LogV2StreamIdValidator we just check here that the steamId is
	// in the right space. The stream does not have to exist.
	public void Validate(StreamId streamId) {
		Ensure.Nonnegative(streamId, "streamId");
	}
}
