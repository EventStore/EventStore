// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.Transport.Http;

public struct RequestParams {
	public readonly bool IsDone;
	public readonly TimeSpan Timeout;

	public RequestParams(bool done) {
		IsDone = done;
		Timeout = TimeSpan.Zero;
	}

	public RequestParams(TimeSpan timeout) {
		IsDone = false;
		Timeout = timeout;
	}

	public RequestParams(bool done, TimeSpan timeout) {
		IsDone = done;
		Timeout = timeout;
	}
}
