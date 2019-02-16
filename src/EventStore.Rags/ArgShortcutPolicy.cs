namespace EventStore.Rags {
	/// <summary>
	/// An enum to represent argument shortcut policies.
	/// </summary>
	public enum ArgShortcutPolicy {
		/// <summary>
		/// No special behavior.
		/// </summary>
		Default,

		/// <summary>
		/// Pass this value to the ArgShortcut attribute's constructor to indicate that the given property
		/// does not support a shortcut.
		/// </summary>
		NoShortcut,

		/// <summary>
		/// This indicates that the .NET property named should not be used as an indicator.  Instead,
		/// only the values in the other ArgShortcut attributes should be used.
		/// </summary>
		ShortcutsOnly,
	}
}
