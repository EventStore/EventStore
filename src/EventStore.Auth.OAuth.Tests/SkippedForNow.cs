// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Xunit;

namespace EventStore.Auth.OAuth.Tests;

public sealed class SkippedForNow : FactAttribute {
	public SkippedForNow() {
		Skip = "Test is skipped for now";
	}
}
