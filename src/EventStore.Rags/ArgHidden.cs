using System;

namespace EventStore.Rags {
	/// <summary>
	/// Provides the ability to hide an option when dumping the options
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ArgHidden : Attribute {
		
	}
}

