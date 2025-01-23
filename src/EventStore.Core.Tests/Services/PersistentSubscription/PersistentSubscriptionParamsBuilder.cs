// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Common.Utils;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests.Services.PersistentSubscription;

public abstract class PersistentSubscriptionParamsBuilder {
	private bool _resolveLinkTos;
	private IPersistentSubscriptionStreamPosition _startFrom;
	private bool _recordStatistics;
	private TimeSpan _timeout;
	private int _readBatchSize;
	private int _maxRetryCount;
	private int _liveBufferSize;
	private int _historyBufferSize;
	private string _subscriptionId;
	private IPersistentSubscriptionEventSource _eventSource;
	private string _groupName;
	private IPersistentSubscriptionStreamReader _streamReader;
	private IPersistentSubscriptionCheckpointReader _checkpointReader;
	private IPersistentSubscriptionCheckpointWriter _checkpointWriter;
	private IPersistentSubscriptionMessageParker _messageParker;
	private TimeSpan _checkPointAfter;
	private int _minCheckPointCount;
	private int _maxCheckPointCount;
	private int _maxSubscriberCount;
	private IPersistentSubscriptionConsumerStrategy _consumerStrategy;

	public PersistentSubscriptionParamsBuilder WithCheckpointReader(IPersistentSubscriptionCheckpointReader reader) {
		_checkpointReader = reader;
		return this;
	}

	public PersistentSubscriptionParamsBuilder SetGroup(string groupName) {
		_groupName = groupName;
		return this;
	}

	public PersistentSubscriptionParamsBuilder SetSubscriptionId(string subscriptionId) {
		_subscriptionId = subscriptionId;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithEventSource(IPersistentSubscriptionEventSource eventSource) {
		_eventSource = eventSource;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithMessageParker(IPersistentSubscriptionMessageParker parker) {
		_messageParker = parker;
		return this;
	}

	public PersistentSubscriptionParamsBuilder
		WithCheckpointWriter(IPersistentSubscriptionCheckpointWriter writer) {
		_checkpointWriter = writer;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithEventLoader(IPersistentSubscriptionStreamReader loader) {
		_streamReader = loader;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithExtraStatistics() {
		_recordStatistics = true;
		return this;
	}

	public PersistentSubscriptionParamsBuilder ResolveLinkTos() {
		_resolveLinkTos = true;
		return this;
	}

	public PersistentSubscriptionParamsBuilder DoNotResolveLinkTos() {
		_resolveLinkTos = false;
		return this;
	}

	public PersistentSubscriptionParamsBuilder PreferRoundRobin() {
		_consumerStrategy = new RoundRobinPersistentSubscriptionConsumerStrategy();
		return this;
	}

	public PersistentSubscriptionParamsBuilder PreferDispatchToSingle() {
		_consumerStrategy = new DispatchToSinglePersistentSubscriptionConsumerStrategy();
		return this;
	}

	public PersistentSubscriptionParamsBuilder CustomConsumerStrategy(IPersistentSubscriptionConsumerStrategy consumerStrategy) {
		_consumerStrategy = consumerStrategy;
		return this;
	}

	public PersistentSubscriptionParamsBuilder StartFrom(IPersistentSubscriptionStreamPosition startFrom) {
		_startFrom = startFrom;
		return this;
	}

	public abstract PersistentSubscriptionParamsBuilder StartFromBeginning();

	public abstract PersistentSubscriptionParamsBuilder StartFromCurrent();

	public PersistentSubscriptionParamsBuilder DontTimeoutMessages() {
		_timeout = TimeSpan.MaxValue;
		return this;
	}

	public PersistentSubscriptionParamsBuilder CheckPointAfter(TimeSpan time) {
		_checkPointAfter = time;
		return this;
	}

	public PersistentSubscriptionParamsBuilder MinimumToCheckPoint(int count) {
		_minCheckPointCount = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder MaximumToCheckPoint(int count) {
		_maxCheckPointCount = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder MaximumSubscribers(int count) {
		_maxSubscriberCount = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithMessageTimeoutOf(TimeSpan timeout) {
		_timeout = timeout;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithMaxRetriesOf(int count) {
		Ensure.Nonnegative(count, "count");
		_maxRetryCount = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithLiveBufferSizeOf(int count) {
		Ensure.Nonnegative(count, "count");
		_liveBufferSize = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithReadBatchOf(int count) {
		Ensure.Nonnegative(count, "count");
		_readBatchSize = count;
		return this;
	}


	public PersistentSubscriptionParamsBuilder WithHistoryBufferSizeOf(int count) {
		Ensure.Nonnegative(count, "count");
		_historyBufferSize = count;
		return this;
	}

	public PersistentSubscriptionParamsBuilder WithNamedConsumerStrategy(IPersistentSubscriptionConsumerStrategy consumerStrategy) {
		Ensure.NotNull(consumerStrategy, "consumerStrategy");
		_consumerStrategy = consumerStrategy;
		return this;
	}

	public static implicit operator PersistentSubscriptionParams(PersistentSubscriptionParamsBuilder builder) {
		return new PersistentSubscriptionParams(builder._resolveLinkTos,
			builder._subscriptionId,
			builder._eventSource,
			builder._groupName,
			builder._startFrom,
			builder._recordStatistics,
			builder._timeout,
			builder._maxRetryCount,
			builder._liveBufferSize,
			builder._historyBufferSize,
			builder._readBatchSize,
			builder._checkPointAfter,
			builder._minCheckPointCount,
			builder._maxCheckPointCount,
			builder._maxSubscriberCount,
			builder._consumerStrategy,
			builder._streamReader,
			builder._checkpointReader,
			builder._checkpointWriter,
			builder._messageParker);
	}
}

public class PersistentSubscriptionToStreamParamsBuilder : PersistentSubscriptionParamsBuilder {
	public static PersistentSubscriptionParamsBuilder CreateFor(string streamName, string groupName) {
		return new PersistentSubscriptionToStreamParamsBuilder()
			.FromStream(streamName)
			.StartFrom(0)
			.SetGroup(groupName)
			.SetSubscriptionId(streamName + ":" + groupName)
			.DoNotResolveLinkTos()
			.WithMessageTimeoutOf(TimeSpan.FromSeconds(30))
			.WithHistoryBufferSizeOf(500)
			.WithLiveBufferSizeOf(500)
			.WithMaxRetriesOf(10)
			.WithReadBatchOf(20)
			.CheckPointAfter(TimeSpan.FromSeconds(1))
			.MinimumToCheckPoint(5)
			.MaximumToCheckPoint(1000)
			.MaximumSubscribers(0)
			.WithNamedConsumerStrategy(new RoundRobinPersistentSubscriptionConsumerStrategy());
	}

	public PersistentSubscriptionToStreamParamsBuilder FromStream(string stream) {
		WithEventSource(new PersistentSubscriptionSingleStreamEventSource(stream));
		return this;
	}

	public PersistentSubscriptionToStreamParamsBuilder StartFrom(long startFrom) {
		StartFrom(new PersistentSubscriptionSingleStreamPosition(startFrom));
		return this;
	}

	public override PersistentSubscriptionParamsBuilder StartFromBeginning() {
		StartFrom(new PersistentSubscriptionSingleStreamPosition(0));
		return this;
	}

	public override PersistentSubscriptionParamsBuilder StartFromCurrent() {
		StartFrom(new PersistentSubscriptionSingleStreamPosition(-1));
		return this;
	}
}

public class PersistentSubscriptionToAllParamsBuilder : PersistentSubscriptionParamsBuilder {
	public static PersistentSubscriptionParamsBuilder CreateFor(string groupName, IEventFilter filter = null) {
		return new PersistentSubscriptionToAllParamsBuilder()
			.FromAll(filter)
			.StartFrom(0L, 0L)
			.SetGroup(groupName)
			.SetSubscriptionId("$all" + ":" + groupName)
			.DoNotResolveLinkTos()
			.WithMessageTimeoutOf(TimeSpan.FromSeconds(30))
			.WithHistoryBufferSizeOf(500)
			.WithLiveBufferSizeOf(500)
			.WithMaxRetriesOf(10)
			.WithReadBatchOf(20)
			.CheckPointAfter(TimeSpan.FromSeconds(1))
			.MinimumToCheckPoint(5)
			.MaximumToCheckPoint(1000)
			.MaximumSubscribers(0)
			.WithNamedConsumerStrategy(new RoundRobinPersistentSubscriptionConsumerStrategy());
	}

	public PersistentSubscriptionToAllParamsBuilder FromAll(IEventFilter filter = null) {
		WithEventSource(new PersistentSubscriptionAllStreamEventSource(filter));
		return this;
	}

	public PersistentSubscriptionToAllParamsBuilder StartFrom(long commitPosition, long preparePosition) {
		StartFrom(new PersistentSubscriptionAllStreamPosition(commitPosition, preparePosition));
		return this;
	}

	public override PersistentSubscriptionParamsBuilder StartFromBeginning() {
		StartFrom(new PersistentSubscriptionAllStreamPosition(0, 0));
		return this;
	}

	public override PersistentSubscriptionParamsBuilder StartFromCurrent() {
		StartFrom(new PersistentSubscriptionAllStreamPosition(-1, -1));
		return this;
	}
}
