using System;


namespace EventStore.Core.Services.RequestManager {
	public class Disposer : IDisposable {
		private Disposer() { _disposed = true; }
		public static Disposer Disposed() { 
			return new Disposer();
		} 
		
		private Action _disposeAction;
		
		public Disposer(Action disposeAction) {
			_disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
		}
		
		private bool _disposed;
		public void Dispose() {
			if (_disposed)
				return;
			try {
				_disposeAction();
				_disposeAction = null;
			} catch {
				//ignore
			}
			_disposed = true;
		}
	}
}
