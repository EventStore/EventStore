// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Cryptography;

namespace EventStore.Licensing.Tests;

public static class Keys {
	static Keys() {
		using var rsa = RSA.Create(512);
		Public = Convert.ToBase64String(rsa.ExportRSAPublicKey());
		Private = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
	}

	public static string Public { get; }
	public static string Private { get; }
}
