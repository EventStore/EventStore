// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Xunit;

namespace EventStore.Auth.Ldaps.Tests;

public sealed class SkipOnWindowsAttribute : FactAttribute {
	public SkipOnWindowsAttribute() {
		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			Skip = "Test skipped on Windows";
	}
}
