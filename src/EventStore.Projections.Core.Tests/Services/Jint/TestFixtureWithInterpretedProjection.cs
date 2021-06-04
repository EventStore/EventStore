﻿using System;
using System.Collections.Generic;
using EventStore.Common;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.Jint
{
	public abstract class TestFixtureWithInterpretedProjection {
		protected ProjectionStateHandlerFactory _stateHandlerFactory;
		protected IProjectionStateHandler _stateHandler;
		protected List<string> _logged;
		protected string _projection;
		protected string _state = null;
		protected string _sharedState = null;
		protected IQuerySources _source;
		protected TimeSpan CompilationTimeout { get; set; } = TimeSpan.FromMilliseconds(1000);
		protected TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

		[SetUp]
		public void Setup() {
			_state = null;
			_projection = null;
			Given();
			_logged = new List<string>();
			_stateHandlerFactory =
				new ProjectionStateHandlerFactory(CompilationTimeout, ExecutionTimeout, JavascriptProjectionRuntime.Interpreted);
			_stateHandler = CreateStateHandler();
			_source = _stateHandler.GetSourceDefinition();

			if (_state != null)
				_stateHandler.Load(_state);
			else
				_stateHandler.Initialize();

			if (_sharedState != null)
				_stateHandler.LoadShared(_sharedState);
			When();
		}

		protected const string _projectionType = "js";

		protected virtual IProjectionStateHandler CreateStateHandler() {
			return _stateHandlerFactory.Create(
				_projectionType, _projection, true, logger: (s, _) => {
					if (s.StartsWith("P:"))
						Console.WriteLine(s);
					else
						_logged.Add(s);
				}); // skip prelude debug output
		}

		protected virtual void When() {
		}

		protected abstract void Given();
	}
}
