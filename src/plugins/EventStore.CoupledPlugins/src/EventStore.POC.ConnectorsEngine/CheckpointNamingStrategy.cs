// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.ConnectorsEngine.Infrastructure;

namespace EventStore.POC.ConnectorsEngine;

public class CheckpointNamingStrategy : INamingStrategy {
	const string Postfix = "-checkpoint";
	private readonly INamingStrategy _inner;

	public CheckpointNamingStrategy(INamingStrategy inner) {
		_inner = inner;
	}

	public string NameFor(string name) => $"{_inner.NameFor(name)}{Postfix}";

	public string IdFor(string name) {
		if (!name.EndsWith(Postfix))
			throw new InvalidOperationException($"Invalid checkpoint stream name {name}");

		return _inner.IdFor(name.Remove(name.Length - Postfix.Length));
	}
}
