using System;

namespace EventStore.Rags {
	public class ArgDescriptionAttribute : Attribute {
		public string Description { get; private set; }
		public string Group { get; private set; }

		public ArgDescriptionAttribute(string description) {
			Description = description;
		}

		public ArgDescriptionAttribute(string description, string group) {
			Description = description;
			Group = @group;
		}
	}
}
