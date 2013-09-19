/*
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Private.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.LeaderNode
{
    public class ElectionServiceHappyCaseLeader
    {
        protected ElectionsServiceUnit ElectionsUnit;

        protected void ProcessElections(IEnumerable<IPEndPoint> doNotRespondWithViewChange = null,
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
                                        .Count(x => x.AttemptedView == 0), Is.EqualTo(2), "View Change 0");
            Assert.That(ElectionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length,
                        Is.EqualTo(2),
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
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.Prepare)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.Prepare>()
                                .Count(x => x.View == 1)
                        , Is.EqualTo(2), "Prepare of view 1");

            Assert.That(ElectionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(1), "Scheduled");
            Assert.That(ElectionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var prepareOkMessages = ElectionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                                                                     && x.State != VNodeState.Manager
                                                                     && !doNotRespondWithPrepareOk.Contains(x.InternalHttpEndPoint))
                .Select(x => new ElectionMessage.PrepareOk(x.InternalHttpEndPoint, 1));

            ElectionsUnit.Publish(prepareOkMessages);

            var master = ElectionsUnit.Publisher.Messages.Where(x => x is HttpMessage.SendOverHttp)
                                                         .Cast<HttpMessage.SendOverHttp>()
                                                         .Select(x => x.Message)
                                                         .Where(x => x is ElectionMessage.Proposal)
                                                         .Cast<ElectionMessage.Proposal>()
                                                         .Last()
                                                         .Master;

            overHttpMessages = ElectionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.Proposal)
                                     .Select(x => x.Message)
                                     .Cast<ElectionMessage.Proposal>()
                                     .Count(x => x.View == 1 && x.Master.Equals(master))
                        , Is.EqualTo(2), "Proposal with master for view 1");
            Assert.That(ElectionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var acceptMessages = ElectionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                                                                  && x.State != VNodeState.Manager
                                                                  && !doNotRespondWithAccept.Contains(x.InternalHttpEndPoint))
                .Select(x => new ElectionMessage.Accept(x.InternalHttpEndPoint, 1, master));

            ElectionsUnit.Publish(acceptMessages);
        }
    }

    [TestFixture]
    public sealed class elections_service_should_complete_happy_case_as_leader_thus : ElectionServiceHappyCaseLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);

            ProcessElections();
        }

        [Test]
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
    public sealed class elections_service_should_complete_happy_case_as_leader_with_2_nodes_alive_thus 
        : ElectionServiceHappyCaseLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);
            
            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);
            ElectionsUnit.UpdateClusterMemberInfo(3, isAlive:false);

            ProcessElections();
        }

        [Test]
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
    public sealed class elections_service_should_complete_happy_case_as_leader_with_2_nodes_accepted_view_thus 
        : ElectionServiceHappyCaseLeader
    {
        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);

            ProcessElections(doNotRespondWithViewChange: new[] { ElectionsUnit.ClusterInfo.Members[0].InternalHttpEndPoint });
        }

        [Test]
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

namespace EventStore.Core.Tests.Services.ElectionsService.LeaderNode
{
}