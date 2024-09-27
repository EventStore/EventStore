// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services.Processing.Strategies;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_filter {
	public class TestFixtureWithEventFilter {
		protected SourceDefinitionBuilder _builder;
		protected EventFilter _ef;
		protected Exception _exception;

		[SetUp]
		public void Setup() {
			_builder = new SourceDefinitionBuilder();
			Given();
			When();
		}

		protected virtual void Given() {
		}

		protected virtual void When() {
			_ef = null;
			try {
				var sources = _builder.Build();
				_ef =
					ReaderStrategy.Create("test", 0, sources, new RealTimeProvider(), stopOnEof: false, runAs: null)
						.EventFilter;
			} catch (Exception ex) {
				_exception = ex;
			}
		}
	}
}
