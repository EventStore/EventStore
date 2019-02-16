using System;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers.IODispatcherTests;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Projections.Core.Messages.Persisted.Responses.Slave;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;
using NUnit.Framework;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.master_core_projection_response_reader {
	public abstract class with_master_core_response_reader : IHandle<ClientMessage.WriteEvents>,
		IHandle<ClientMessage.ReadStreamEventsForward> {
		protected Guid _workerId = Guid.NewGuid();
		protected Guid _masterProjectionId = Guid.NewGuid();
		protected string _streamId;

		protected MasterCoreProjectionResponseReader _reader;
		protected InMemoryBus _bus;
		protected IODispatcher _ioDispatcher;

		[OneTimeSetUp]
		protected virtual void TestFixtureSetUp() {
			_streamId = "$projection-$" + _masterProjectionId.ToString("N");

			_bus = InMemoryBus.CreateTest();
			_bus.Subscribe<ClientMessage.WriteEvents>(this);
			_bus.Subscribe<ClientMessage.ReadStreamEventsForward>(this);

			_ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
			IODispatcherTestHelpers.SubscribeIODispatcher(_ioDispatcher, _bus);

			_reader = new MasterCoreProjectionResponseReader(_bus, _ioDispatcher, _workerId, _masterProjectionId);
		}

		public abstract void Handle(ClientMessage.ReadStreamEventsForward message);

		public virtual void Handle(ClientMessage.WriteEvents message) {
			var response = new ClientMessage.WriteEventsCompleted(message.CorrelationId, 0, 0, 1000, 1000);
			message.Envelope.ReplyWith(response);
		}

		public ClientMessage.ReadStreamEventsForwardCompleted CreateResultCommandReadResponse(
			ClientMessage.ReadStreamEventsForward message) {
			var result = new PartitionProcessingResultResponse {
				SubscriptionId = Guid.NewGuid().ToString("N"),
				Partition = "teststream",
				CausedBy = Guid.NewGuid().ToString("N"),
				Position = CheckpointTag.Empty,
				Result = "result"
			};
			var data = JsonConvert.SerializeObject(result);

			var evnts = IODispatcherTestHelpers.CreateResolvedEvent(_streamId, "$result", data);
			return new ClientMessage.ReadStreamEventsForwardCompleted(message.CorrelationId, message.EventStreamId,
				message.FromEventNumber, message.MaxCount,
				ReadStreamResult.Success, evnts, null, false, String.Empty, message.FromEventNumber + 1,
				message.FromEventNumber, true, 10000);
		}
	}
}
