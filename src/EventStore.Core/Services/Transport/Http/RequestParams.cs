// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

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
