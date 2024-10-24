// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Tests.Fakes;

public class FakeTfReader : ITransactionFileReader {
	public void Reposition(long position) {
		throw new NotImplementedException();
	}

	public ValueTask<SeqReadResult> TryReadNext(CancellationToken token)
		=> ValueTask.FromException<SeqReadResult>(new NotImplementedException());

	public ValueTask<SeqReadResult> TryReadPrev(CancellationToken token)
		=> ValueTask.FromException<SeqReadResult>(new NotImplementedException());

	public ValueTask<RecordReadResult> TryReadAt(long position, bool couldBeScavenged, CancellationToken token)
		=> ValueTask.FromException<RecordReadResult>(new NotImplementedException());

	public ValueTask<bool> ExistsAt(long position, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled<bool>(token) : ValueTask.FromResult(true);
}
