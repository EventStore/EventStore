// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace EventStore.Auth.OAuth.Tests;

internal class XUnitLoggerFactory : ILoggerFactory {
	private readonly ITestOutputHelper _output;

	public XUnitLoggerFactory(ITestOutputHelper output) {
		_output = output;
	}

	public void Dispose() {
	}

	public ILogger CreateLogger(string categoryName) => new XUnitLogger(categoryName, _output);

	public void AddProvider(ILoggerProvider provider) {
	}

	private class XUnitLogger : ILogger {
		private readonly string _categoryName;
		private readonly ITestOutputHelper _output;

		public XUnitLogger(string categoryName, ITestOutputHelper output) {
			_categoryName = categoryName;
			_output = output;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter) => _output
			.WriteLine($"[{logLevel}] [{_categoryName}] {formatter(state, exception)}");

		public bool IsEnabled(LogLevel logLevel) => true;

		public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

		private class NoOpDisposable : IDisposable {
			public void Dispose() { }
		}
	}
}
