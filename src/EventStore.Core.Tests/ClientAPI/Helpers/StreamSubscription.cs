extern alias GrpcClientStreams;
extern alias GrpcClient;
using InternalStreamSubscription = GrpcClientStreams.EventStore.Client.StreamSubscription;
using SubscriptionDroppedReason = GrpcClient.EventStore.Client.SubscriptionDroppedReason;
using System;
using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace EventStore.Core.Tests.ClientAPI.Helpers; 
public class StreamSubscription : IDisposable {
	private int _dropped = 0;
	public Guid SubscriptionId { get; init; }
	public InternalStreamSubscription Internal {  get; set; }

	public bool IsDropped => Interlocked.CompareExchange(ref _dropped, 1, 1) == 1;

	public Action<StreamSubscription, SubscriptionDroppedReason, Exception> SubscriptionDropped { get; set; }

	public StreamSubscription() {
		SubscriptionId = Guid.NewGuid();
	}

	public void Dispose() {
		if (Interlocked.CompareExchange(ref _dropped, 1, 0) != 0)
			return;

		if (Internal != null)
			Internal.Dispose();
		else
			SubscriptionDropped?.Invoke(this, SubscriptionDroppedReason.Disposed, null);
	}

	public void ReportSubscriberError(Exception ex) {
		ReportDropped(SubscriptionDroppedReason.SubscriberError, ex);
	}

	public void ReportDropped(SubscriptionDroppedReason reason, Exception ex) {
		if (Interlocked.CompareExchange(ref _dropped, 1, 0) != 0)
			return;

		SubscriptionDropped?.Invoke(this, reason, ex);
	}
}
