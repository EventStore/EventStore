using System;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace EventStore.Core.Duck.Default;

public sealed class DefaultIndexHandler : IEventHandler, IDisposable {
	readonly DuckDBConnection _connection;
	readonly SemaphoreSlim _semaphore = new(1);

	ulong _seq;
	DuckDBAppender _appender;

	public DefaultIndexHandler() {
		_connection = DuckDb.Connection;
		_appender = _connection.CreateAppender("idx_all");
		var last = DefaultIndex.GetLastPosition();
		_seq = last.HasValue ? last.Value + 1 : 0;
	}

	public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
		if (context.Stream.ToString().StartsWith('$')) return ValueTask.FromResult(EventHandlingStatus.Ignored);

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

		return ValueTask.FromResult(EventHandlingStatus.Success);
	}

	public string DiagnosticName => "DefaultIndexHandler";

	public void Dispose() {
		_semaphore.Dispose();
	}

	public void Commit() {
		_semaphore.Wait();
		_appender.Dispose();
		_appender = _connection.CreateAppender("idx_all");
		_semaphore.Release();
	}
}
