using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager {
	public class DelaySendService : IDisposable {
		//todo: allocate in a pool and reuse
		public struct MessageNode {
			public readonly long Position;
			public readonly Message Message;
			public MessageNode(long position, Message message) {
				Position = position;
				Message = message;
			}
		}
		private readonly LinkedList<MessageNode> _registeredMessages = new LinkedList<MessageNode>();
		private readonly Thread _thread;
		private object _registerLock = new object();
		private readonly AutoResetEvent _wakeEvent = new AutoResetEvent(true);
		private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(false);
		private Action<Message> _target;
		private CancellationTokenSource _cancelSource;
		private CancellationToken _canceled;
		private bool _disposed;

		public DelaySendService(Action<Message> target) {
			_cancelSource = new CancellationTokenSource();
			_canceled = _cancelSource.Token;
			_thread = new Thread(Notify) { IsBackground = true, Name = nameof(DelaySendService) };
			_thread.Start();
			_target = target;
		}
		private void Publish(Message message) {
			try {
				_target(message);
			} catch {

				//ignore;
			}
		}
		private void Notify() {
			while (!_canceled.IsCancellationRequested) {
				Notify(DateTime.UtcNow.ToTicksSinceEpoch());
				if (_registeredMessages.IsEmpty()) {
					_wakeEvent.WaitOne();
				} else {
					var delayTicks = _registeredMessages.First.Value.Position - DateTime.UtcNow.ToTicksSinceEpoch();
					if (delayTicks > 0) { _wakeEvent.WaitOne(TimeSpan.FromTicks(delayTicks)); }
				}
			}
			_stopped.Set();
		}
		private void Notify(long logPosition) {			
			lock (_registerLock) {
				var node = _registeredMessages.First;
				while (node != null && node.Value.Position <= logPosition && !_canceled.IsCancellationRequested) {
					var next = node.Next;
					Publish(node.Value.Message);
					_registeredMessages.Remove(node);
					node = next;
				}
			}
		}

		public void DelaySend(TimeSpan delay, Message message) {
			lock (_registerLock) {
				if (delay < TimeSpan.FromMilliseconds(2)) {
					Publish(message);
					return;
				};
				var position = (DateTime.UtcNow + delay).ToTicksSinceEpoch();
				var node = new MessageNode(position, message);
				if (_registeredMessages.IsEmpty() || _registeredMessages.First.Value.Position >= position) {
					_registeredMessages.AddFirst(node);
					_wakeEvent.Set();
					return;
				}
				if (_registeredMessages.Last.Value.Position <= position) {
					_registeredMessages.AddLast(node);
					return;
				}

				//todo: better search needed
				var root = _registeredMessages.First;
				while (root.Value.Position <= position) {
					root = root.Next;
				}
				_registeredMessages.AddAfter(root, node);
			}
		}

		protected virtual void Dispose(bool disposing) {
			if (!_disposed) {
				if (disposing) {
					_cancelSource.Cancel();
					_cancelSource.Dispose();
					if (_stopped.Wait(TimeSpan.FromMilliseconds(250))) {
						_registeredMessages.Clear();
					}
					_target = null;
				}
				_disposed = true;
			}
		}
		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
