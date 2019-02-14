using System;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	[TestFixture]
	public class when_creating {
		[Test]
		public void can_be_created() {
			TestHelper.Consume(new ParallelProcessingLoadBalancer(2, 10, 1));
		}

		[Test]
		public void zero_workers_throws_argument_exception() {
			Assert.Throws<ArgumentException>(
				() => { TestHelper.Consume(new ParallelProcessingLoadBalancer(0, 10, 1)); });
		}

		[Test]
		public void negative_workers_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				TestHelper.Consume(new ParallelProcessingLoadBalancer(-1, 10, 1));
			});
		}

		[Test]
		public void zero_max_scheduled_size_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				TestHelper.Consume(new ParallelProcessingLoadBalancer(2, 0, 1));
			});
		}

		[Test]
		public void negative_max_scheduled_size_throws_argument_exception() {
			Assert.Throws<ArgumentException>(
				() => { TestHelper.Consume(new ParallelProcessingLoadBalancer(2, -1, 1)); });
		}

		[Test]
		public void zero_max_unmeasured_tasks_throws_argument_exception() {
			Assert.Throws<ArgumentException>(
				() => { TestHelper.Consume(new ParallelProcessingLoadBalancer(2, 10, 0)); });
		}

		[Test]
		public void negative_max_unmeasured_tasks_throws_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				TestHelper.Consume(new ParallelProcessingLoadBalancer(2, 10, -2));
			});
		}
	}
}
