// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.Core.Tests;

public sealed class Disposables : IDisposable {
	private readonly List<IDisposable> _disposables = new();

	public void Dispose() {
		for (int i = _disposables.Count - 1; i >= 0; i--) {
			_disposables[i]?.Dispose();
		}
	}

	public void Add(IDisposable disposable) =>
		_disposables.Add(disposable);
}
