/*using System;
using System.Linq;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Private.Cluster;
using EventStore.Core.Private.Messages;
using EventStore.Core.Services.TimerService;
using NUnit.Framework;

namespace EventStore.Core.Private.Tests.Services.ElectionsService.NonLeaderNode
{
    public sealed class paxos_walktrough_as_nonleader
    {
        private ElectionsServiceUnit _electionsUnit;
        private MemberInfo _leaderForViewChange1;

        [SetUp]
        public void SetUp()
        {
            var clusterSettingsFactory = new ClusterSettingsFactory();
            var clusterSettings = clusterSettingsFactory.GetClusterSettings(0, 3);

            _electionsUnit = new ElectionsServiceUnit(clusterSettings);
            _leaderForViewChange1 = _electionsUnit.GetNodeAt(1);

            var gossipUpdate = new GossipMessage.GossipUpdated(_electionsUnit.ClusterInfo);
            _electionsUnit.Publish(gossipUpdate);
        }

        [Test]
        public void should_send_view_change_0_after_elections_started()
        {
            _electionsUnit.Publish(new ElectionMessage.StartElections());

            var overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 0)
                        , Is.EqualTo(2), "View Change 0");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(2),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");
        }

        [Test]
        public void should_send_view_change_1()
        {
            _electionsUnit.Publish(new ElectionMessage.StartElections());

            var overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 0)
                        , Is.EqualTo(2), "View Change 0");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(2),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var viewChangeMessages = _electionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(_electionsUnit.OwnEndPoint)
                                                             && x.State != VNodeState.Manager)
                                                   .Select(x => new ElectionMessage.ViewChange(x.InternalHttpEndPoint, 1));

            _electionsUnit.Publish(viewChangeMessages);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 1)
                        , Is.EqualTo(2), "View Change 1");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(1),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");
        }

        [Test]
        public void should_send_prepare_ok_to_leader_after_prepare_received()
        {
            _electionsUnit.Publish(new ElectionMessage.StartElections());

            var overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 0)
                        , Is.EqualTo(2), "View Change 0");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(2),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var viewChangeMessages = _electionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(_electionsUnit.OwnEndPoint)
                                                             && x.State != VNodeState.Manager)
                                                   .Select(x => new ElectionMessage.ViewChange(x.InternalHttpEndPoint, 1));

            _electionsUnit.Publish(viewChangeMessages);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 1)
                        , Is.EqualTo(2), "View Change 1");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(1),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var prepareMessage = new ElectionMessage.Prepare(_leaderForViewChange1.InternalHttpEndPoint, 1);
            _electionsUnit.Publish(prepareMessage);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.EndPoint.Equals(_leaderForViewChange1.InternalHttpEndPoint)
                                                  && x.Message is ElectionMessage.PrepareOk)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.PrepareOk>()
                                .Count(x => x.View == 1)
                        , Is.EqualTo(1), "Prepare Ok for view 1 response to leader");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");
        }


        [Test]
        public void should_send_accept_after_proposal_for_view_received()
        {
            var expectedMaster = _electionsUnit.GetNodeAt(2);

            _electionsUnit.Publish(new ElectionMessage.StartElections());

            var overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 0)
                        , Is.EqualTo(2), "View Change 0");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(2),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var viewChangeMessages = _electionsUnit.ListAliveMembers(x => !x.InternalHttpEndPoint.Equals(_electionsUnit.OwnEndPoint)
                                                                      && x.State != VNodeState.Manager)
                .Select(x => new ElectionMessage.ViewChange(x.InternalHttpEndPoint, 1));

            _electionsUnit.Publish(viewChangeMessages);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.Message is ElectionMessage.ViewChange)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.ViewChange>()
                                .Count(x => x.AttemptedView == 1)
                        , Is.EqualTo(2), "View Change 1");
            Assert.That(_electionsUnit.ClearMessageFromQueue<TimerMessage.Schedule>().Length, Is.EqualTo(1),
                "Scheduled published count");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");


            var prepareMessage = new ElectionMessage.Prepare(_leaderForViewChange1.InternalHttpEndPoint, 1);
            _electionsUnit.Publish(prepareMessage);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.EndPoint.Equals(_leaderForViewChange1.InternalHttpEndPoint)
                                                  && x.Message is ElectionMessage.PrepareOk)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.PrepareOk>()
                                .Count(x => x.View == 1)
                        , Is.EqualTo(1), "Prepare Ok for view 1 response to leader");
            Assert.That(_electionsUnit.Publisher.Messages.Count, Is.EqualTo(0), "Remaining messages");

            var proposalMessage = new ElectionMessage.Proposal(_leaderForViewChange1.InternalHttpEndPoint,
                                                               1,
                                                               expectedMaster.InternalHttpEndPoint,
                                                               0, 
                                                               -1, -1, Guid.Empty);
            _electionsUnit.Publish(proposalMessage);

            overHttpMessages = _electionsUnit.ClearMessageFromQueue<HttpMessage.SendOverHttp>();
            Assert.That(overHttpMessages.Where(x => x.EndPoint.Equals(_leaderForViewChange1.InternalHttpEndPoint)
                                                  && x.Message is ElectionMessage.Accept)
                                .Select(x => x.Message)
                                .Cast<ElectionMessage.Accept>()
                                .Count(x => x.View == 1 && x.MasterId == expectedMaster.InstanceId)
                        , Is.EqualTo(1), "Accept for view 1 to leader with expected master");
        }
    }
}*/

namespace EventStore.Core.Tests.Services.ElectionsService.NonLeaderNode
{
}