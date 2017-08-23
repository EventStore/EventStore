using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EventStore.Core.Tests.Integration
{
    [TestFixture, Category("LongRunning")]
    public class when_a_master_is_shutdown : specification_with_cluster
    {
        private List<Guid> _epochIds = new List<Guid>();
        private CountdownEvent _expectedNumberOfRoleAssignments;
        protected override void BeforeNodesStart()
        {
            _nodes.ToList().ForEach(x =>
                x.Node.MainBus.Subscribe(new AdHocHandler<SystemMessage.StateChangeMessage>(Handle)));
            _expectedNumberOfRoleAssignments = new CountdownEvent(3);
            base.BeforeNodesStart();
        }

        protected override void Given()
        {
            _expectedNumberOfRoleAssignments.Wait(5000);
            var master = _nodes.First(x => x.NodeState == Data.VNodeState.Master);
            ShutdownNode(master.DebugIndex);
            _expectedNumberOfRoleAssignments = new CountdownEvent(2);
            _expectedNumberOfRoleAssignments.Wait(5000);
            base.Given();
        }

        private void Handle(SystemMessage.StateChangeMessage msg)
        {
            switch (msg.State)
            {
                case Data.VNodeState.Master:
                    _expectedNumberOfRoleAssignments?.Signal();
                    _epochIds.Add(((SystemMessage.BecomeMaster)msg).EpochId);
                    break;
                case Data.VNodeState.Slave:
                    _expectedNumberOfRoleAssignments?.Signal();
                    _epochIds.Add(((SystemMessage.BecomeSlave)msg).EpochId);
                    break;
            }
        }

        [Test]
        public void should_use_the_same_epochIds()
        {
            Assert.AreEqual(1, _epochIds.Take(3).Distinct().Count());
            Assert.AreEqual(1, _epochIds.Skip(3).Take(2).Distinct().Count());
        } 
    }
}
