using System;

namespace EventStore.Client {
	/// <summary>
	/// Exception thrown when a required metadata property is missing.
	/// </summary>
	public class RequiredMetadataPropertyMissingException : Exception {
		public RequiredMetadataPropertyMissingException(string missingMetadataProperty, Exception innerException) :
			base($"Required metadata property {missingMetadataProperty} is missing", innerException) {
		}
	}
}
