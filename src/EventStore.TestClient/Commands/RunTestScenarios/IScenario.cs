using System;

namespace EventStore.TestClient.Commands.RunTestScenarios {
	internal interface IScenario : IDisposable {
		void Run();
		void Clean();
	}
}
