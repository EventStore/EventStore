using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Parses a version string following that format X[.Y[.Z]]
	/// </summary>
	public class StringCompatibilityMode : ICompatibilityMode {
	private readonly int _majorNum;

	/// <summary>
	/// Constructs a <see cref="StringCompatibilityMode"/>.
	/// </summary>
	/// <param name="compatibilityModeStr"></param>
	public StringCompatibilityMode(string compatibilityModeStr) {
		Ensure.NotNullOrEmpty(compatibilityModeStr, "compatibilityModeStr");
		var splits = compatibilityModeStr.Split('.');

		if (!int.TryParse(splits[0], out var majNum)) {
			throw new ArgumentException($"Invalid compability version string {compatibilityModeStr}");
		}

		_majorNum = majNum;
	}

	/// <summary>
	/// Is EventStoreDB Version 5 compatibility mode enabled.
	/// </summary>
	public bool IsVersion5CompatibilityModeEnabled() {
		return _majorNum == 5;
	}
	}
}
