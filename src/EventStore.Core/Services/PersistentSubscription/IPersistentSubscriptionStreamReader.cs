// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription;

public interface IPersistentSubscriptionStreamReader {
	void BeginReadEvents(IPersistentSubscriptionEventSource eventSource,
		IPersistentSubscriptionStreamPosition startPosition, int countToLoad, int batchSize, int maxWindowSize,
		bool resolveLinkTos, bool skipFirstEvent,
		Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onEventsFound,
		Action<IPersistentSubscriptionStreamPosition, long> onEventsSkipped,
		Action<string> onError);
}
