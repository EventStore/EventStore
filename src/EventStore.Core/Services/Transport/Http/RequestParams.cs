// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.Transport.Http;

public struct RequestParams(bool done, TimeSpan timeout) {
	public readonly bool IsDone = done;
	public readonly TimeSpan Timeout = timeout;

	public RequestParams(bool done) : this(done, TimeSpan.Zero) {
	}

	public RequestParams(TimeSpan timeout) : this(false, timeout) {
	}
}
