// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using System.Collections;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources;

public class KurrentEnvironmentVariablesSource(IDictionary? environment = null) : IConfigurationSource {
	public IConfigurationProvider Build(IConfigurationBuilder builder) =>
		new KurrentEnvironmentVariablesConfigurationProvider(environment);
}
