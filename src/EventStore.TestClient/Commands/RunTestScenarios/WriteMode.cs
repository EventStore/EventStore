namespace EventStore.TestClient.Commands.RunTestScenarios {
	internal enum WriteMode {
		SingleEventAtTime,
		Bucket,
		Transactional
	}
}
