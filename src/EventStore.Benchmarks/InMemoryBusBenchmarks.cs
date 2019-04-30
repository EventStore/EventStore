using System.Threading;
using BenchmarkDotNet.Attributes;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Benchmarks
{
	public class InMemoryBusBenchmarks {
		private readonly InMemoryBus _bus;
		private readonly SingleHierarchy _singleHierarchy;

		public InMemoryBusBenchmarks() {
			_bus = new InMemoryBus("test");
			_bus.Subscribe(new NoopHandler<Message>());
			_singleHierarchy = new SingleHierarchy();
		}

		[Benchmark(OperationsPerInvoke = 10000)]
		public void DispatchSingleHierarchy() {
			for (var i = 0; i < 10000; i++) _bus.Handle(_singleHierarchy);
		}

		private class SingleHierarchy : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
			public override int MsgTypeId => TypeId;
		}

		private class NoopHandler<T> : IHandle<T> where T : Message {
			public void Handle(T msg) {
			}
		}
	}
}
