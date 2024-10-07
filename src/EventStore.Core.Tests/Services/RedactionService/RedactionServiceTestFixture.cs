// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Services;
using EventStore.Core.Synchronization;
using EventStore.Core.Tests.Bus;
using EventStore.Core.Tests.Services.Storage;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RedactionService;

[TestFixture]
public abstract class RedactionServiceTestFixture<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
	private SemaphoreSlimLock _switchChunksLock;
	public RedactionService<TStreamId> RedactionService { get; private set; }

	public RedactionServiceTestFixture() : base(chunkSize: 1024) { }

	[SetUp]
	public virtual Task SetUp() {
		_switchChunksLock = new SemaphoreSlimLock();
		RedactionService = new RedactionService<TStreamId>(new FakeQueuedHandler(), Db, ReadIndex, _switchChunksLock);
		return Task.CompletedTask;
	}

	[TearDown]
	public virtual Task TearDown() {
		_switchChunksLock?.Dispose();
		return Task.CompletedTask;
	}
}
