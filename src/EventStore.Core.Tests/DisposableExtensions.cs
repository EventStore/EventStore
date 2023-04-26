using System;

namespace EventStore.Core.Tests;

public static class DisposableExtensions {
	public static T DisposeWith<T>(this T disposable, Disposables disposables)
		where T : IDisposable {

		disposables.Add(disposable);
		return disposable;
	}
}
