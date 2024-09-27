// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Exceptions {
	public class HashValidationException : Exception {
		public HashValidationException() {
		}

		public HashValidationException(string message) : base(message) {
		}

		public HashValidationException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
