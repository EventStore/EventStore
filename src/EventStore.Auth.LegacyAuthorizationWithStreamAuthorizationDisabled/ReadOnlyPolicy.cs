// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

internal class ReadOnlyPolicy {
	private readonly IReadOnlyDictionary<OperationDefinition, ReadOnlyMemory<IAssertion>> _assertions;
	private readonly string _name;
	private readonly DateTimeOffset _validFrom;
	private readonly long _version;

	public ReadOnlyPolicy(string name, long version, DateTimeOffset validFrom,
		IReadOnlyDictionary<OperationDefinition, ReadOnlyMemory<IAssertion>> assertions) {
		_name = name;
		_version = version;
		_validFrom = validFrom;
		_assertions = assertions;
	}

	public PolicyInformation Information => new PolicyInformation(_name, _version, DateTimeOffset.MaxValue);

	public bool TryGetAssertions(OperationDefinition operation, out ReadOnlyMemory<IAssertion> assertions) {
		return _assertions.TryGetValue(operation, out assertions);
	}
}
