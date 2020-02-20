using System;
using System.Threading.Tasks;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Integration {
	public class when_a_single_node_is_shutdown : SpecificationWithDirectory {
		[Test]
		public async Task cancels_after_timeout() {
			var node = new MiniNode(PathName);
			try {
				await node.Start();

				var shutdownTask = node.Node.StopAsync(TimeSpan.FromMilliseconds(1));
				await Task.Delay(100);
				Assert.True(shutdownTask.IsCanceled);
			} finally {
				await node.Shutdown();
			}
		}
	}
}
