using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.ClientAPI;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services {
	public abstract class SpecificationWithEmittedStreamsTrackerAndDeleter : SpecificationWithMiniNode {
		protected IEmittedStreamsTracker _emittedStreamsTracker;
		protected IEmittedStreamsDeleter _emittedStreamsDeleter;
		protected ProjectionNamesBuilder _projectionNamesBuilder;
		protected ClientMessage.ReadStreamEventsForwardCompleted _readCompleted;
		protected IODispatcher _ioDispatcher;
		protected bool _trackEmittedStreams = true;
		protected string _projectionName = "test_projection";

		protected override void Given() {
			_ioDispatcher = new IODispatcher(_node.Node.MainQueue, new PublishEnvelope(_node.Node.MainQueue));
			_node.Node.MainBus.Subscribe(_ioDispatcher.BackwardReader);
			_node.Node.MainBus.Subscribe(_ioDispatcher.ForwardReader);
			_node.Node.MainBus.Subscribe(_ioDispatcher.Writer);
			_node.Node.MainBus.Subscribe(_ioDispatcher.StreamDeleter);
			_node.Node.MainBus.Subscribe(_ioDispatcher.Awaker);
			_node.Node.MainBus.Subscribe(_ioDispatcher);
			_projectionNamesBuilder = ProjectionNamesBuilder.CreateForTest(_projectionName);
			_emittedStreamsTracker = new EmittedStreamsTracker(_ioDispatcher,
				new ProjectionConfig(null, 1000, 1000 * 1000, 100, 500, true, true, false, false, false,
					_trackEmittedStreams, 10000, 1), _projectionNamesBuilder);
			_emittedStreamsDeleter = new EmittedStreamsDeleter(_ioDispatcher,
				_projectionNamesBuilder.GetEmittedStreamsName(),
				_projectionNamesBuilder.GetEmittedStreamsCheckpointName());
		}
	}
}
