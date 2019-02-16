using System;

namespace EventStore.Rags {
	/// <summary>
	/// Set an alias or aliases for an argument.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ArgAliasAttribute : Attribute {
		public string[] Aliases { get; private set; }

		public ArgAliasAttribute(params string[] aliases) {
			this.Aliases = aliases;
		}
	}
}
