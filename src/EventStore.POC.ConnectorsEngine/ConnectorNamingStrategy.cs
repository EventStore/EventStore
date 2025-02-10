// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.ConnectorsEngine.Infrastructure;

namespace EventStore.POC.ConnectorsEngine;

public class ConnectorNamingStrategy : INamingStrategy {
	const string Prefix = ConnectorConsts.ConnectorDefinitionStreamCategory;
	public string NameFor(string name) => $"{Prefix}{name}";
	public string IdFor(string name) {
		if (!name.StartsWith(Prefix))
			throw new InvalidOperationException($"Invalid connector stream name {name}");

		return name[Prefix.Length..];
	}
}
