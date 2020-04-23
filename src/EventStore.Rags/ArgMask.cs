using System;

namespace EventStore.Rags {
	/// <summary>
	/// Provides the ability to mask an option when dumping the options
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ArgMask : Attribute {
		
	}
}
