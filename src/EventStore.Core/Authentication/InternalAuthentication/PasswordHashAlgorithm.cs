// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Authentication.InternalAuthentication;

public abstract class PasswordHashAlgorithm {
	public abstract void Hash(string password, out string hash, out string salt);
	public abstract bool Verify(string password, string hash, string salt);
}
