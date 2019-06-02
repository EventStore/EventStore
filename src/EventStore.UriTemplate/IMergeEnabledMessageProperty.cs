namespace EventStore.UriTemplate {
	internal interface IMergeEnabledMessageProperty {
		bool TryMergeWithProperty(object propertyToMerge);
	}
}
