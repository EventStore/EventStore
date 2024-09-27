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
