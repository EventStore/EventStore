// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Index {
	public interface IIndexScavengerLog {
		void IndexTableScavenged(int level, int index, TimeSpan elapsed, long entriesDeleted, long entriesKept,
			long spaceSaved);

		void IndexTableNotScavenged(int level, int index, TimeSpan elapsed, long entriesKept, string errorMessage);
	}
}
