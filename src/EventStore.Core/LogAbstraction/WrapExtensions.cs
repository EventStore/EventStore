using System;

namespace EventStore.Core.LogAbstraction {
	public static class WrapExtensions {
		public static U Wrap<T, U>(this T t, Func<T, U> wrap) => wrap(t);
	}
}
