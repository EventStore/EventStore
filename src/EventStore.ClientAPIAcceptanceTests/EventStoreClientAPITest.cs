using System;
using System.Runtime.CompilerServices;

namespace EventStore.ClientAPIAcceptanceTests {
	public abstract class EventStoreClientAPITest {
		public string GetStreamName([CallerMemberName] string testMethod = default) 
			=> $"{GetType().Name}_{testMethod ?? "unknown"}";
	}
}
