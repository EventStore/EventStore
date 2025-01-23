// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Transport.Http;

internal class OptionalAuthorizationLevel(AuthorizationLevel authorizationLevel) {
	private readonly AuthorizationLevel _authorizationLevel = authorizationLevel;

	public static implicit operator AuthorizationLevel(OptionalAuthorizationLevel level) => level._authorizationLevel;
}
