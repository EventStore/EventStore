using System;

namespace EventStore.Rags {
	/// <summary>
	/// Use this attribute to override the shortcut that PowerArgs automatically assigns to each property.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
	public class ArgShortcut : Attribute {
		/// <summary>
		/// The shortcut for the given property.
		/// </summary>
		public string Shortcut { get; private set; }

		/// <summary>
		/// Get the ShortcutPolicy for this attribute.
		/// </summary>
		public ArgShortcutPolicy Policy { get; private set; }

		/// <summary>
		/// Creates a new ArgShortcut attribute with a specified value.
		/// </summary>
		/// <param name="shortcut">The value of the new shortcut.</param>
		public ArgShortcut(string shortcut) {
			this.Shortcut = shortcut;
			this.Policy = ArgShortcutPolicy.Default;
		}

		/// <summary>
		/// Creates a new ArgShortcut using the given policy.
		/// </summary>
		/// <param name="policy"></param>
		public ArgShortcut(ArgShortcutPolicy policy) {
			this.Policy = policy;
		}
	}
}
