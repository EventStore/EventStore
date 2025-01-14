// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Cryptography;
using EventStore.Plugins;
using EventStore.Plugins.MD5;

namespace EventStore.MD5;

internal class MD5Provider : Plugin, IMD5Provider {
	public MD5Provider() : base(requiredEntitlements: ["MD5"]) {
	}

	public HashAlgorithm Create() => MD5.Create();
}
