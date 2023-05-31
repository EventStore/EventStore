using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using Serilog.Core;
using Serilog.Events;

namespace EventStore.Core;

public class LogsStreamPublisher : ILogEventSink {
	private readonly IPublisher _mainBus;
	private const PrepareFlags Flags = PrepareFlags.Data | PrepareFlags.IsCommitted | PrepareFlags.IsJson;
	private long _eventNumber;

	public LogsStreamPublisher(IPublisher mainBus) {
		_mainBus = mainBus;
		_eventNumber = 0;
	}

	public void Emit(LogEvent logEvent) {
		var log = new {
			Message = logEvent.RenderMessage(),
			logEvent.Level,
			logEvent.Timestamp,
		};

		var data = JsonSerializer.SerializeToUtf8Bytes(log);
		var prepare = new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "$logs", _eventNumber, DateTime.Now, Flags, "log-emitted", data, Array.Empty<byte>());
		var @event = new EventRecord(_eventNumber, prepare, "$logs", "log-emitted");

		_mainBus.Publish(new StorageMessage.EventCommitted(0, @event, false));
		_eventNumber++;
	}
}
