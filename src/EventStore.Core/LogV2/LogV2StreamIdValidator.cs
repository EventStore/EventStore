// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;

namespace EventStore.Core.LogV2;

public class LogV2StreamIdValidator : IValidator<string> {
	public LogV2StreamIdValidator() {
	}

	public void Validate(string streamId) {
		Ensure.NotNullOrEmpty(streamId, "streamId");
	}
}
