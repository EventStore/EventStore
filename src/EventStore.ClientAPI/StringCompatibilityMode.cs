using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Parses a version string following that format X[.Y[.Z]]
	/// </summary>
	public class StringCompatibilityMode : ICompatibilityMode {
		private readonly int? _majorNum;
		private readonly bool _auto;

		/// <summary>
		/// Constructs a <see cref="StringCompatibilityMode"/>.
		/// </summary>
		/// <param name="compatibilityModeStr"></param>
		public StringCompatibilityMode(string compatibilityModeStr) {
			Ensure.NotNullOrEmpty(compatibilityModeStr, "compatibilityModeStr");
			var splits = compatibilityModeStr.Split('.');

			if (int.TryParse(splits[0], out var majNum)) {
				_majorNum = majNum;
			} else if (compatibilityModeStr.Trim().ToLowerInvariant() == "auto") {
				_auto = true;
			}
		}

		/// <summary>
		/// Is EventStoreDB Auto compatibility mode enabled.
		/// </summary>
		public bool IsAutoCompatibilityModeEnabled() {
			return _auto;
		}

		/// <summary>
		/// Is EventStoreDB Version 5 compatibility mode enabled.
		/// </summary>
		public bool IsVersion5CompatibilityModeEnabled() {
			return _majorNum == 5;
		}
	}
}
