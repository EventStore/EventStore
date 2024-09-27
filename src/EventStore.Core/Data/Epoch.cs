// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Data {
	public class Epoch {
		public readonly long EpochPosition;
		public readonly int EpochNumber;
		public readonly Guid EpochId;

		public Epoch(long epochPosition, int epochNumber, Guid epochId) {
			EpochPosition = epochPosition;
			EpochNumber = epochNumber;
			EpochId = epochId;
		}
	}
}
