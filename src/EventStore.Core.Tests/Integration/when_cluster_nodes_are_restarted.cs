using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Data;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;

namespace EventStore.Core.Tests.Integration {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_restarting_one_node_at_a_time<TLogFormat, TStreamId> : specification_with_cluster<TLogFormat, TStreamId> {
		protected override async Task Given() {
			await base.Given();

			for (int i = 0; i < 9; i++) {
				await _nodes[i % 3].Shutdown(keepDb: true);
				await Task.Delay(2000);
				
				var node = CreateNode(i % 3, _nodeEndpoints[i % 3],
					new[] {_nodeEndpoints[(i+1)%3].HttpEndPoint, _nodeEndpoints[(i+2)%3].HttpEndPoint});
				node.Start();
				_nodes[i % 3] = node;

				await Task.WhenAll(_nodes.Select(x => x.Started)).WithTimeout(TimeSpan.FromSeconds(30));
			}
		}

		[Test]
		public void cluster_should_stabilize() {
			var leaders = 0;
			var followers = 0;

			for (int i = 0; i < 3; i++) {
				var state = _nodes[i].NodeState;
				if (state == VNodeState.Leader) leaders++;
				else if (state == VNodeState.Follower) followers++;
			}

			Assert.AreEqual(1, leaders);
			Assert.AreEqual(2, followers);
		}
	}
}
