using System;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Serilog;

namespace EventStore.Core.Duck.Default;

public sealed class DefaultIndexHandler : IEventHandler, IDisposable {
	readonly DuckDBConnection _connection;
	readonly SemaphoreSlim _semaphore = new(1);

	ulong _seq;
	int _page;
	DuckDBAppender _appender;

	static readonly ILogger Logger = Log.ForContext<DefaultIndexHandler>();

	public DefaultIndexHandler() {
		_connection = DuckDb.Connection;
		_appender = _connection.CreateAppender("idx_all");
		var last = DefaultIndex.GetLastSequence();
		Logger.Information("Last known global sequence: {Seq}", last);
		_seq = last.HasValue ? last.Value + 1 : 0;
	}

	public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
		if (context.Stream.ToString().StartsWith('$'))
			return ValueTask.FromResult(EventHandlingStatus.Ignored);

		if (_appenderDisposed || _disposing) return new(EventHandlingStatus.Ignored);

		var streamId = StreamIndex.Handle(context);
		var et = EventTypeIndex.Handle(context);
		var cat = CategoryIndex.Handle(context);

		_semaphore.Wait();
		var row = _appender.CreateRow();
		row.AppendValue(_seq++);
		row.AppendValue((int)context.EventNumber);
		row.AppendValue(context.GlobalPosition);
		row.AppendValue(context.Created);
		row.AppendValue(streamId);
		row.AppendValue((int)et.Id);
		row.AppendValue(et.Sequence);
		row.AppendValue((int)cat.Id);
		row.AppendValue(cat.Sequence);
		row.EndRow();
		_semaphore.Release();
		_page++;

		return ValueTask.FromResult(EventHandlingStatus.Success);
	}

	public string DiagnosticName => "DefaultIndexHandler";

	bool _disposed;
	bool _disposing;
	bool _appenderDisposed;

	public void Dispose() {
		if (_disposing || _disposed) return;
		_disposing = true;
		Commit(false);
		_semaphore.Dispose();
		_disposed = true;
	}

	public void Commit(bool reopen = true) {
		if (_appenderDisposed) return;
		_semaphore.Wait();
		_appender.Dispose();
		_appenderDisposed = true;
		Logger.Information("Committed {Count} records to index at sequence {Seq}", _page, _seq);
		_page = 0;
		if (!reopen) return;
		_appender = _connection.CreateAppender("idx_all");
		_semaphore.Release();
		_appenderDisposed = false;
	}

	public ulong GetLastPosition() => _seq;
}
