// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text.Json;
using EventStore.POC.IO.Core;
using Json.Path;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing;

public interface IFilter {
	bool Evaluate(Event e);

	class None : IFilter {
		public static None Instance = new();

		protected None() {
		}

		public virtual bool Evaluate(Event e) => true;

		public override string ToString() {
			return "None";
		}
	}
}

class JsonPathFilter : IFilter {
	private readonly ILogger _logger;
	private readonly JsonPath _filter;
	private readonly JsonSerializerOptions _serializerOptions;


	public JsonPathFilter(string filter, ILogger logger) {
		try {
			_filter = JsonPath.Parse(filter);
		} catch (PathParseException ex) {
			throw new Exception("Invalid Filter configuration:", ex);
		}

		_logger = logger;

		_serializerOptions = new JsonSerializerOptions() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
	}

	public bool Evaluate(Event e) {
		var doc = e.ToJson(_logger);
		var result = _filter.Evaluate(doc);
		return result.Matches is { Count: > 0 };
	}

	public override string ToString() {
		return $"JsonPath:{_filter}";
	}
}
