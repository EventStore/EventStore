using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_response_writer {
	abstract class specification_with_projection_core_service_response_writer {
		protected ProjectionCoreResponseWriter _sut;
		protected List<Tuple<string, object>> _publishedCommands;
		private IResponseWriter _writer;

		[SetUp]
		public void SetUp() {
			_publishedCommands = new List<Tuple<string, object>>();
			_writer = new FakeWriter(this);
			_sut = new ProjectionCoreResponseWriter(_writer);
			Given();
			When();
		}

		protected T AssertParsedSingleCommand<T>(string command) {
			Assert.AreEqual(1, _publishedCommands.Count);
			Assert.AreEqual(command, _publishedCommands[0].Item1);
			Assert.IsInstanceOf<T>(_publishedCommands[0].Item2);
			var source = (T)_publishedCommands[0].Item2;
			var serialized = source.ToJson();
			var parsed = serialized.ParseJson<T>();
			return parsed;
		}

		protected virtual void Given() {
		}

		protected abstract void When();

		class FakeWriter : IResponseWriter {
			private readonly specification_with_projection_core_service_response_writer _container;

			public FakeWriter(specification_with_projection_core_service_response_writer container) {
				_container = container;
			}

			public void PublishCommand(string command, object body) {
				_container.PublishCommand(command, body);
			}

			public void Reset() {
			}
		}

		private void PublishCommand(string command, object body) {
			_publishedCommands.Add(Tuple.Create(command, body));
		}
	}
}
