extern alias GrpcClientStreams;
extern alias GrpcClient;
using InternalStreamSubscription = GrpcClientStreams.EventStore.Client.StreamSubscription;
using SubscriptionDroppedReason = GrpcClient.EventStore.Client.SubscriptionDroppedReason;
using System;
using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace EventStore.Core.Tests.ClientAPI.Helpers; 
public class StreamSubscription : IDisposable {
	public Guid SubscriptionId { get; init; }
	private InternalStreamSubscription _internal;
	private Action<StreamSubscription, SubscriptionDroppedReason, Exception> _subscriptionDropped;
	public InternalStreamSubscription Internal {
		get { return _internal; }

		set {
			_internal = value;
			CancellationTokenSource.Token.Register(() => _internal.Dispose());
		}
	}

	public CancellationTokenSource CancellationTokenSource { get; init; }

	public Action<StreamSubscription, SubscriptionDroppedReason, Exception> SubscriptionDropped {
		get { return _subscriptionDropped; }
		set {
			_subscriptionDropped = value;
			CancellationTokenSource.Token.Register(() => {
				if (_internal == null) {
					_subscriptionDropped(this, SubscriptionDroppedReason.Disposed, null);
				}
			});
		}
	}

	public StreamSubscription() {
		SubscriptionId = Guid.NewGuid();
		CancellationTokenSource = new CancellationTokenSource();
	}

	public void Dispose() {
		CancellationTokenSource.Cancel();
	}

	public void ReportSubscriberError(Exception ex) {
		ReportDropped(SubscriptionDroppedReason.SubscriberError, ex);
	}

	public void ReportDropped(SubscriptionDroppedReason reason, Exception ex) {
		SubscriptionDropped?.Invoke(this, reason, ex);
	}
}
