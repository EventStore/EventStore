// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests.Helpers {
	public class AbstractorFactoryConfigurator<TStreamId> : ILogFormatAbstractorFactory<TStreamId> {
		private readonly ILogFormatAbstractorFactory<TStreamId> _wrapped;
		private readonly Func<LogFormatAbstractorOptions, LogFormatAbstractorOptions> _configure;

		public AbstractorFactoryConfigurator(
			ILogFormatAbstractorFactory<TStreamId> wrapped,
			Func<LogFormatAbstractorOptions, LogFormatAbstractorOptions> configure) {
			_wrapped = wrapped;
			_configure = configure;
		}

		public LogFormatAbstractor<TStreamId> Create(LogFormatAbstractorOptions options) =>
			_wrapped.Create(_configure(options));
	}

	public static class LogFormatFactoryExtensions {
		public static ILogFormatAbstractorFactory<TStreamId> Configure<TStreamId>(
			this ILogFormatAbstractorFactory<TStreamId> factory,
			Func<LogFormatAbstractorOptions, LogFormatAbstractorOptions> configure) =>

			new AbstractorFactoryConfigurator<TStreamId>(factory, configure);
	}
}
