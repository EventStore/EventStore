// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Exceptions;

internal class UnableToReadPastEndOfStreamException : Exception {
	public UnableToReadPastEndOfStreamException() {
	}

	public UnableToReadPastEndOfStreamException(string message) : base(message) {
	}

	public UnableToReadPastEndOfStreamException(string message, Exception innerException) : base(message,
		innerException) {
	}
}
