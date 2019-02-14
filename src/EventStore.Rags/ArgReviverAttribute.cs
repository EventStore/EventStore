using System;

namespace EventStore.Rags {
	/// <summary>
	/// The attribute used when you want to create an arg reviver. You add this to public static methods 
	/// that take 2 string parameters (the first represents the name of the property, the second represents the string value
	/// and the return type is the type that you are reviving (or converting) the string into.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class ArgReviverAttribute : Attribute {
	}
}
