using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.ClientOperations;

namespace EventStore.Core.Tests.Helpers {
	public static class TestHelper {
		public static void Consume<T>(T v) {
		}
		public static Task<T> WaitForNext<T>(this ISubscriber bus) where T : Message {			
			var tcs = new TaskCompletionSource<T>();
			bus.Subscribe(new AdHocHandler<T>( msg=> tcs.TrySetResult(msg)));
			return tcs.Task;
		}
	}
}
