using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.command_writer {
	abstract class specification_with_projection_manager_command_writer {
		protected ProjectionManagerCommandWriter _sut;
		protected List<Tuple<string, Guid, object>> _publishedCommands;
		private IMultiStreamMessageWriter _writer;

		[SetUp]
		public void SetUp() {
			_publishedCommands = new List<Tuple<string, Guid, object>>();
			_writer = new FakeWriter(this);
			_sut = new ProjectionManagerCommandWriter(_writer);
			Given();
			When();
		}

		protected T AssertParsedSingleCommand<T>(string command, Guid workerId) {
			Assert.AreEqual(1, _publishedCommands.Count);
			Assert.AreEqual(command, _publishedCommands[0].Item1);
			Assert.AreEqual(workerId, _publishedCommands[0].Item2);
			Assert.IsInstanceOf<T>(_publishedCommands[0].Item3);
			var source = (T)_publishedCommands[0].Item3;
			var serialized = source.ToJson();
			var parsed = serialized.ParseJson<T>();
			return parsed;
		}

		protected virtual void Given() {
		}

		protected abstract void When();

		class FakeWriter : IMultiStreamMessageWriter {
			private readonly specification_with_projection_manager_command_writer _container;

			public FakeWriter(specification_with_projection_manager_command_writer container) {
				_container = container;
			}

			public void PublishResponse(string command, Guid workerId, object body) {
				_container.PublishCommand(command, workerId, body);
			}

			public void Reset() {
			}
		}

		private void PublishCommand(string command, Guid workerId, object body) {
			_publishedCommands.Add(Tuple.Create(command, workerId, body));
		}
	}
}
