using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Integration {
	public abstract class SpecificationWithIntegrationVNode : IPublisher, ISubscriber, IDisposable {
		private ClusterVNode _node;

		public void CreateTestNode() {
			var builder = IntegrationVNodeBuilder
			.AsSingleNode()			
			.RunInMemory();

			_node = builder.Build();
			_node.StartAndWaitUntilReady().Wait();
		}
		public void Publish(Message message) {
			_node.MainQueue.Handle(message);
		}

		public void Subscribe<T>(IHandle<T> handler) where T : Message {
			_node.MainBus.Subscribe<T>(handler);
		}

		public void Unsubscribe<T>(IHandle<T> handler) where T : Message {
			_node.MainBus.Unsubscribe<T>(handler);
		}

		#region IDisposable Support
		private bool _disposed = false;
		protected virtual void Dispose(bool disposing) {
			if (!_disposed) {
				if (disposing) {
					// TODO: dispose managed state (managed objects).
					_node.Stop().Wait();
				}
				// TODO: set large fields to null.
				_disposed = true;
			}
		}

		public void Dispose() {
			Dispose(true);
		}
		#endregion
	}
	internal class IntegrationVNodeBuilder : VNodeBuilder {
		protected IntegrationVNodeBuilder() {

		}
		public static IntegrationVNodeBuilder AsSingleNode() {
			var ret = new IntegrationVNodeBuilder().WithSingleNodeSettings();
			return (IntegrationVNodeBuilder)ret;
		}
		protected override void SetUpProjectionsIfNeeded() {
			return;
		}
	}
}
