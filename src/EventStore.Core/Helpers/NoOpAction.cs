using System;

namespace EventStore.Core.Helpers {
	public class NoOpAction {
		public static Action Instance { get; } = () => { };
	}

	public class NoOpAction<T> {
		public static Action<T> Instance { get; } = _ => { };
	}

	public static class ActionExtensions {
		public static Action OrNoOp(this Action self) => self ?? NoOpAction.Instance;
		public static Action<T> OrNoOp<T>(this Action<T> self) => self ?? NoOpAction<T>.Instance;
	}
}
