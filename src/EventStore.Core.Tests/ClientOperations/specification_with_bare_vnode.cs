﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.ClientOperations {
	public abstract class specification_with_bare_vnode : IPublisher, ISubscriber, IDisposable {
		private ClusterVNode _node;
		private List<IDisposable> _disposables = new List<IDisposable>();
		public void CreateTestNode() {
			var builder = IntegrationVNodeBuilder
			.AsSingleNode()
			.RunInMemory();

			_node = builder.Build();
			_node.StartAsync(true).Wait();
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
		public Task<T> WaitForNext<T>() where T : Message {			
			var tcs = new TaskCompletionSource<T>();
			var handler = new TaskHandler<T>(_node.MainBus);
			_disposables.Add(handler);
			return handler.Message;
		}
		private sealed class TaskHandler<T> : IHandle<T>, IDisposable where T : Message {
			private readonly ISubscriber _source;
			private TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();
			public Task<T> Message => _tcs.Task;
			public void Handle(T message) {
				_tcs.SetResult(message);
				Dispose();
			}
			public TaskHandler(ISubscriber source) {
				_source = source;
				_source.Subscribe(this);
			}
			private bool _disposed;
			public void Dispose() {
				if (_disposed) { return; }
				_disposed = true;
				try {
					_source.Unsubscribe(this);
				} catch (Exception) { }
			}
		}
		#region IDisposable Support
		private bool _disposed = false;
		protected virtual void Dispose(bool disposing) {
			if (!_disposed) {
				if (disposing) {					
					_disposables?.ForEach(d => d?.Dispose());
					_disposables?.Clear();
					_node?.StopAsync().Wait();
				}				
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
