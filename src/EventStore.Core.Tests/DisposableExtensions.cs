// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Tests;

public static class DisposableExtensions {
	public static T DisposeWith<T>(this T disposable, Disposables disposables)
		where T : IDisposable {

		disposables.Add(disposable);
		return disposable;
	}
}
