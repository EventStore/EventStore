using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Bus;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Services.Transport.Tcp;

namespace EventStore.Core.Tests.ClientOperations {
	public abstract class specification_with_bare_vnode<TLogFormat, TStreamId> : IPublisher, ISubscriber, IDisposable {
		private ClusterVNode _node;
		private readonly List<IDisposable> _disposables = new List<IDisposable>();
		public void CreateTestNode() {
			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			_node = new ClusterVNode<TStreamId>(new ClusterVNodeOptions()
					.RunInMemory()
					.Secure(new X509Certificate2Collection(ssl_connections.GetRootCertificate()),
						ssl_connections.GetServerCertificate()), logFormat,
				new AuthenticationProviderFactory(c => new InternalAuthenticationProviderFactory(c)),
				new AuthorizationProviderFactory(c => new LegacyAuthorizationProviderFactory(c.MainQueue)));
			_node.StartAsync(true).Wait();
		}
		public void Publish(Message message) {
			_node.MainQueue.Handle(message);
		}

		public void Subscribe<T>(IHandle<T> handler) where T : Message {
			_node.MainBus.Subscribe(handler);
		}

		public void Unsubscribe<T>(IHandle<T> handler) where T : Message {
			_node.MainBus.Unsubscribe(handler);
		}
		public Task<T> WaitForNext<T>() where T : Message {			
			var handler = new TaskHandler<T>(_node.MainBus);
			_disposables.Add(handler);
			return handler.Message;
		}
		private sealed class TaskHandler<T> : IHandle<T>, IDisposable where T : Message {
			private readonly ISubscriber _source;
			private readonly TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();
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
				} catch (Exception) { /* ignore*/}
			}
		}
		#region IDisposable Support
		private bool _disposed;
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
}
