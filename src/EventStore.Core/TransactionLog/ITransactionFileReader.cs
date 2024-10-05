// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileReader {
	void Reposition(long position);

	SeqReadResult TryReadNext();
	ValueTask<SeqReadResult> TryReadPrev(CancellationToken token);

	RecordReadResult TryReadAt(long position, bool couldBeScavenged);
	bool ExistsAt(long position);
}

public struct TFReaderLease : IDisposable {
	public readonly ITransactionFileReader Reader;
	private readonly ObjectPool<ITransactionFileReader> _pool;

	public TFReaderLease(ObjectPool<ITransactionFileReader> pool) {
		_pool = pool;
		Reader = pool.Get();
	}

	public TFReaderLease(ITransactionFileReader reader) {
		_pool = null;
		Reader = reader;
	}

	void IDisposable.Dispose() {
		if (_pool != null)
			_pool.Return(Reader);
	}

	public void Reposition(long position) {
		Reader.Reposition(position);
	}

	public SeqReadResult TryReadNext() {
		return Reader.TryReadNext();
	}

	public ValueTask<SeqReadResult> TryReadPrev(CancellationToken token) {
		return Reader.TryReadPrev(token);
	}

	public bool ExistsAt(long position) {
		return Reader.ExistsAt(position);
	}

	public RecordReadResult TryReadAt(long position, bool couldBeScavenged) {
		return Reader.TryReadAt(position, couldBeScavenged);
	}
}
