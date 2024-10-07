// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Exceptions;

public class MaybeCorruptIndexException : Exception {
	public MaybeCorruptIndexException() {
	}

	public MaybeCorruptIndexException(Exception innerException) : base("May be corrupted index.", innerException) {
	}

	public MaybeCorruptIndexException(string message) : base(message) {
	}

	public MaybeCorruptIndexException(string message, Exception innerException) : base(message, innerException) {
	}
}
