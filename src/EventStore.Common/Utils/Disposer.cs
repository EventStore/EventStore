using System;

namespace EventStore.Common.Utils {

	public sealed class Disposer : IDisposable {
		private Action _disposeFunc;
		public static Disposer EmptyDisposer() => new Disposer();
		private Disposer(){ }
		public Disposer(Action disposeAction) {
			_disposeFunc = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
		}

		private bool _disposed;
		public void Dispose() {
			if (_disposed)
				return;
			try {
				_disposeFunc?.Invoke();
				_disposeFunc = null;
			} catch {
				//ignore
			}
			_disposed = true;
		}
	}

}
