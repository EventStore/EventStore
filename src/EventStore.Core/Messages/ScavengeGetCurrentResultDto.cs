// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Messages;

public class ScavengeGetCurrentResultDto {
	public string ScavengeId { get; set; } = "";
	public string ScavengeLink { get; set; } = "";

	public override string ToString() =>
		$"ScavengeId: {ScavengeId}, " +
		$"ScavengeLink: {ScavengeLink}";
}
