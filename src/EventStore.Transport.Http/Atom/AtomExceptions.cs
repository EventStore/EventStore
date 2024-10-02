// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Transport.Http.Atom {
	public class AtomSpecificationViolationException : Exception {
		public AtomSpecificationViolationException(string message) : base(message) {
		}
	}

	public class ThrowHelper {
		public static void ThrowSpecificationViolation(string message) {
			throw new AtomSpecificationViolationException(message);
		}
	}
}
