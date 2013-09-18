/*using System.Linq;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Private.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.LeaderNode
{
    [TestFixture]
// ReSharper disable InconsistentNaming
    public sealed class paxos_walktrough_as_leader
    {
        private ElectionsServiceUnit ElectionsUnit;

        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(1, 3);

            ElectionsUnit = new ElectionsServiceUnit(clusterSettings);

            var gossipUpdate = new GossipMessage.GossipUpdated(ElectionsUnit.ClusterInfo);
            ElectionsUnit.Publish(gossipUpdate);
        }



        [Test]
        [Category("Network")]
        public void should_send_view_change_0_after_elections_started()
        {
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
        }

        [Test]
        [Category("Network")]
        public void should_publish_prepare_after_view_change_1_published()
        {
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

            var viewChangeMessages = ElectionsUnit.ListAliveMembers(
                x => !x.ExternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                     && x.State != VNodeState.Manager)
                .Select(x => new ElectionMessage.ViewChange(x.ExternalHttpEndPoint, 1));

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
        }

        [Test]
        [Category("Network")]
        public void should_send_proposal_after_prepareok_received()
        {
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

            var viewChangeMessages = ElectionsUnit.ListAliveMembers(
                x => !x.ExternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                     && x.State != VNodeState.Manager)
                .Select(x => new ElectionMessage.ViewChange(x.ExternalHttpEndPoint, 1));

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

            var prepareOkMessages = ElectionsUnit.ListAliveMembers(
                x => !x.ExternalHttpEndPoint.Equals(ElectionsUnit.OwnEndPoint)
                     && x.State != VNodeState.Manager)
                .Select(x => new ElectionMessage.PrepareOk(x.ExternalHttpEndPoint, 1));

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
        }
    }
// ReSharper restore InconsistentNaming
}*/

namespace EventStore.Core.Tests.Services.ElectionsService.LeaderNode
{
}