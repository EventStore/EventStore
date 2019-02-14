using System;

namespace EventStore.Projections.Core.Utils {
	public static class EnumExtensions {
		public static string EnumValueName<T>(this T value) where T : struct {
			return Enum.GetName(typeof(T), value);
		}
	}
}
