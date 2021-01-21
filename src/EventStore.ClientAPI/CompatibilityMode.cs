using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Compatibility mode factory.
	/// </summary>
	public static class CompatibilityMode {
		/// <summary>
		/// Creates a compatibility mode handler. If the string parameter is null or empty, the client will not run any
		/// compatibility mode.
		/// </summary>
		public static ICompatibilityMode Create(string compatibilityModeStr) {
			if (String.IsNullOrEmpty(compatibilityModeStr)) {
				return new NoCompatibilityMode();
			}

			return new StringCompatibilityMode(compatibilityModeStr);
		}
	}
}
