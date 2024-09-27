// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Messaging {
	public struct AdHocCorrelatedTimeout : ICorrelatedTimeout {
		private readonly Action<Guid> _timeout;

		public AdHocCorrelatedTimeout(Action<Guid> timeout) {
			_timeout = timeout;
		}

		public void Timeout(Guid correlationId) {
			_timeout(correlationId);
		}
	}
}
