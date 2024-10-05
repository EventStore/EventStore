// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Tests.Fakes;

public class FakeTfReader : ITransactionFileReader {
	public void Reposition(long position) {
		throw new NotImplementedException();
	}

	public SeqReadResult TryReadNext() {
		throw new NotImplementedException();
	}

	public SeqReadResult TryReadPrev() {
		throw new NotImplementedException();
	}

	public RecordReadResult TryReadAt(long position, bool couldBeScavenged) {
		throw new NotImplementedException();
	}

	public bool ExistsAt(long position) {
		return true;
	}
}
