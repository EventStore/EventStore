// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Messages {
	public class ScavengeGetLastResultDto {
		public string ScavengeId { get; set; } = Guid.Empty.ToString();
		public string ScavengeLink { get; set; } = "";
		public string ScavengeResult { get; set; } = "";

		public override string ToString() =>
			$"ScavengeId: {ScavengeId}, " +
			$"ScavengeLink: {ScavengeLink}, " +
			$"ScavengeResult: {ScavengeResult}";
	}
}
