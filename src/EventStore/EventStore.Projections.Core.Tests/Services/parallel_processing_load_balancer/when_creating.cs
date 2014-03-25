using System;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer
{
    [TestFixture]
    public class when_creating
    {
        [Test]
        public void can_be_created()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_workers_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(0, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_workers_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(-1, 10, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_max_scheduled_size_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 0, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_max_scheduled_size_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, -1, 1);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void zero_max_unmeasured_tasks_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, 0);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void negative_max_unmeasured_tasks_throws_argument_exception()
        {
            var b = new ParallelProcessingLoadBalancer(2, 10, -2);
        }


    }
}
