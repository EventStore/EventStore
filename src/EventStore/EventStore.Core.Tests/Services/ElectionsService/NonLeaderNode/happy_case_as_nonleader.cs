/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Private.Cluster;
using EventStore.Core.Private.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.NonLeaderNode
{
    public class ElectionServiceHappyCaseNonLeader
    {
        protected ElectionsServiceUnit ElectionsUnit;
        protected MemberInfo LeaderForViewChange1;

        protected void ProcessElections(MemberInfo expectedMaster, IEnumerable<IPEndPoint> doNotRespondWithViewChange = null,
                                        IEnumerable<IPEndPoint> doNotRespondWithPrepareOk = null,
                                        IEnumerable<IPEndPoint> doNotRespondWithAccept = null)
        {
            doNotRespondWithViewChange = new HashSet<IPEndPoint>(doNotRespondWithViewChange ?? new IPEndPoint[0]);
            doNotRespondWithPrepareOk = new HashSet<IPEndPoint>(doNotRespondWithPrepareOk ?? new IPEndPoint[0]);
            doNotRespondWithAccept = new HashSet<IPEndPoint>(doNotRespondWithAccept ?? new IPEndPoint[0]);

            var gossipUpdate = new GossipMessage.GossipUpdated(ElectionsUnit.ClusterInfo);
            ElectionsUnit.Publish(gossipUpdate);

            ElectionsUnit.Publish(new ElectionMessage.StartElections());

            var overHttpMessages = ElectionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 0)
                        , Is.EqualTo(2), "View Change 0");
            Assert.That(ElectionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(2),
                "Scheduled published count");
            Assert.That(ElectionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var viewChangeMessages = ElectionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                                                                      && x.State != VNodeState.Manager
                                                                      && !doNotRespondWithViewChange.Contains(x.InternalHttpEndPoint))
                .Select(x => new ElectionMessage.ViewChange(x.InternalHttpEndPoint, 1));

            ElectionsUnit.Publish(viewChangeMessages);

            overHttpMessages = ElectionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 1)
                        , Is.EqualTo(2), "View Change 1");
            Assert.That(ElectionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(1),
                "Scheduled published count");
            Assert.That(ElectionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");


            var prepareMessage = new ElectionMessage.Prepare(LeaderForViewChange1.InternalHttpEndPoint, 1);
            ElectionsUnit.Publish(prepareMessage);

            overHttpMessages = ElectionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.EndPoint.Equals(LeaderForViewChange1.InternalHttpEndPoint)
                                                  && x.Message is ElectionMessage.PrepareOk)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.PrepareOk>()
                                .Count(x => x.View == 1)
                        , Is.EqualTo(1), "Prepare Ok for view 1 response to leader");
            Assert.That(ElectionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var proposalMessage = new ElectionMessage.Proposal(LeaderForViewChange1.InternalHttpEndPoint, 
                                                               1,
                                                               expectedMaster.InternalHttpEndPoint,
                                                               0, 
                                                               -1, -1, Guid.Empty);
            ElectionsUnit.Publish(proposalMessage);

            overHttpMessages = ElectionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.EndPoint.Equals(LeaderForViewChange1.InternalHttpEndPoint)
                                                  && x.Message is ElectionMessage.Accept)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.Accept>()
                                .Count(x => x.View == 1 && x.Master.Equals(expectedMaster.InternalHttpEndPoint))
                        , Is.EqualTo(1), "Accept for view 1 to leader with expected master");
        }
    }

    [TestFixture]
    public sealed class elections_service_should_complete_happy_case_as_nonleader_thus : ElectionServiceHappyCaseNonLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(0, 3);

            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);
            LeaderForViewChange1 = ElectionsUnit.GetNodeAt(1);

            ProcessElections(ElectionsUnit.GetNodeAt(2));
        }

        [Test]
        [Category("Network")]
        public void elect_node_with_biggest_port_ip_for_equal_writerchecksums()
        {
            var lastMessage = ElectionsUnit.Publisher.Messages.Last();
            Assert.That(lastMessage, Is.InstanceOf<ElectionMessage.ElectionsDone>());

            var expectedMaster = ElectionsUnit.ClusterInfo.Members.Last();
            var electionsDone = (ElectionMessage.ElectionsDone)lastMessage;
            Assert.That(electionsDone.Master, Is.EqualTo(expectedMaster));
        }
    }

    [TestFixture]
    public sealed class elections_service_should_complete_happy_case_as_nonleader_with_2_nodes_alive_thus 
        : ElectionServiceHappyCaseNonLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(0, 3);
            
            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);
            ElectionsUnit.UpdateClusterMemberInfo(3, isAlive:false);
            LeaderForViewChange1 = ElectionsUnit.GetNodeAt(1);

            ProcessElections(ElectionsUnit.GetNodeAt(1));
        }

        [Test]
        [Category("Network")]
        public void elect_node_with_biggest_port_ip_for_equal_writerchecksums()
        {
            var lastMessage = ElectionsUnit.Publisher.Messages.Last();
            Assert.That(lastMessage, Is.InstanceOf<ElectionMessage.ElectionsDone>());

            var expectedMaster = ElectionsUnit.GetNodeAt(1);
            var electionsDone = (ElectionMessage.ElectionsDone)lastMessage;
            Assert.That(electionsDone.Master, Is.EqualTo(expectedMaster));
        }
    }

    [TestFixture]
    public sealed class elections_service_should_complete_happy_case_as_nonleader_with_2_nodes_accepted_view_thus 
        : ElectionServiceHappyCaseNonLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(0, 3);

            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);
            LeaderForViewChange1 = ElectionsUnit.GetNodeAt(1);

            ProcessElections(ElectionsUnit.GetNodeAt(2),
                             doNotRespondWithViewChange: new[] { ElectionsUnit.GetNodeAt(2).InternalHttpEndPoint });
        }

        [Test]
        [Category("Network")]
        public void elect_node_with_biggest_port_ip_for_equal_writerchecksums()
        {
            var lastMessage = ElectionsUnit.Publisher.Messages.Last();
            Assert.That(lastMessage, Is.InstanceOf<ElectionMessage.ElectionsDone>());

            var expectedMaster = ElectionsUnit.ClusterInfo.Members.Last();
            var electionsDone = (ElectionMessage.ElectionsDone)lastMessage;
            Assert.That(electionsDone.Master, Is.EqualTo(expectedMaster));
        }
    }


}
*/

namespace EventStore.Core.Tests.Services.ElectionsService.NonLeaderNode
{
}