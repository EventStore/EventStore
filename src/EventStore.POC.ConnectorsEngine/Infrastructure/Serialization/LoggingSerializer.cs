// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public class LoggingSerializer : ISerializer {
	private readonly ISerializer _wrapped;
	private readonly ILogger _logger;

	public LoggingSerializer(ISerializer wrapped, ILogger logger) {
		_wrapped = wrapped;
		_logger = logger;
	}

	public EventToWrite Serialize(Message evt) {
		return _wrapped.Serialize(evt);
	}

	public bool TryDeserialize(Event evt, out Message? message, out Exception? exception) {
		if (!_wrapped.TryDeserialize(evt, out message, out exception)) {
			_logger.Warning(exception, "Could not deserialize {eventType} event {number}@{stream}",
				evt.EventType, evt.EventNumber, evt.Stream);
			return false;
		}
		return true;
	}
}
