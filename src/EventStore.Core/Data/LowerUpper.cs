// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data {
	public struct Range {
		public readonly long Lower;
		public readonly long Upper;

		public Range(long lower, long upper) {
			Lower = lower;
			Upper = upper;
		}
	}
}
