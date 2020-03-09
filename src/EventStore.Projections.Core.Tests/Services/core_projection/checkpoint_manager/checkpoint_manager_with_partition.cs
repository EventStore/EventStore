using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager {
	[TestFixture]
	public class checkpoint_manager_with_partition :
		TestFixtureWithCoreProjectionCheckpointManager {
		[Test]
		public void when_loading_partition_state_for_a_partition() {
			var checkpointMetadata = @"{
				  ""$v"": ""1:-1:0:1"",
				  ""$s"": {
					""$ce-Evnt"": 0
				  }
				}";
			PartitionState state = new PartitionState("{\"foo\":1}", "{\"bar\":1}", CheckpointTag.Empty);
			var serializedState = state.Serialize();
			var partition = "abc";
			ExistingEvent(_namingBuilder.MakePartitionCheckpointStreamName(partition), "$Checkpoint",
				checkpointMetadata, serializedState);
			_manager.BeginLoadPartitionStateAt(partition, CheckpointTag.Empty, s => {
				Assert.AreEqual(serializedState, s.Serialize());
			});
		}
	}
}
