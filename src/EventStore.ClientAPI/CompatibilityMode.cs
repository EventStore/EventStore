using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Compatibility mode factory.
	/// </summary>
	public static class CompatibilityMode {
		/// <summary>
		/// Creates a compatibility mode handler. If the string parameter is "disabled", the client will run v5
		/// compatibility mode (to avoid breaking changes).
		/// </summary>
		public static ICompatibilityMode Create(string compatibilityModeStr) {
			Ensure.NotNullOrEmpty(compatibilityModeStr, "compatibilityModeStr");

			if (String.Equals(compatibilityModeStr.ToLower(), "disabled")) {
				return new Version5CompatibilityMode();
			}

			return new StringCompatibilityMode(compatibilityModeStr);
		}
	}
}
