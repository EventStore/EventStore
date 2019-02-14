using EventStore.Core.Data;

namespace EventStore.TestClient.Commands.DvuBasic {
	public interface IBasicProducer {
		string Name { get; }

		Event Create(int version);
		bool Equal(int eventVersion, string eventType, byte[] actualData);
	}
}
