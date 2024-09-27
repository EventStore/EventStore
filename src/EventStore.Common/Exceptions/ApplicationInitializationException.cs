// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Exceptions {
	public class ApplicationInitializationException : Exception {
		public ApplicationInitializationException(string message) : base(message) {
		}

		public ApplicationInitializationException(string message, Exception innerException) : base(message,
			innerException) {
		}
	}
}
