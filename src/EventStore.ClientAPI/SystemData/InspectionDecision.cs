namespace EventStore.ClientAPI.SystemData {
	internal enum InspectionDecision {
		DoNothing,
		EndOperation,
		Retry,
		Reconnect,
		Subscribed
	}
}
