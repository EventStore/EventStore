using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Tests;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.ClientAPI
{
    [Category("ClientAPI")]
    public class specification_with_standard_projections_runnning : SpecificationWithDirectoryPerTestFixture
    {
        protected MiniNode _node;
        protected IEventStoreConnection _conn;
        protected ProjectionsSubsystem _projections;
        protected UserCredentials _admin = new UserCredentials("admin", "changeit");
        protected ProjectionsManager _manager;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
#if DEBUG
            QueueStatsCollector.InitializeIdleDetection();
#else 
            throw new NotSupportedException("These tests require DEBUG conditional");
#endif
            CreateNode();

            _conn = EventStoreConnection.Create(_node.TcpEndPoint);
            _conn.Connect();

            _manager = new ProjectionsManager(new ConsoleLogger(), _node.HttpEndPoint, TimeSpan.FromMilliseconds(10000));
            WaitIdle();
            if (GivenStandardProjectionsRunning())
                EnableStandardProjections();
            QueueStatsCollector.WaitIdle();
            Given();
            When();
        }

        private void CreateNode()
        {
            _projections = new ProjectionsSubsystem(1, runProjections: RunProjections.All);
            _node = new MiniNode(
                PathName, inMemDb: true, skipInitializeStandardUsersCheck: false, subsystems: new ISubsystem[] {_projections});
            _node.Start();
        }

        [TearDown]
        public void PostTestAsserts()
        {
            var all = _manager.ListAll(_admin);
            if (all.Contains("Faulted"))
                Assert.Fail("Projections faulted while running the test" + "\r\n" + all);
        }

        protected void EnableStandardProjections()
        {
            EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
            EnableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
        }

        protected void DisableStandardProjections()
        {
            DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByCategoryStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.EventByTypeStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamByCategoryStandardProjection);
            DisableProjection(ProjectionNamesBuilder.StandardProjections.StreamsStandardProjection);
        }

        protected virtual bool GivenStandardProjectionsRunning()
        {
            return true;
        }

        protected void EnableProjection(string name)
        {
            _manager.Enable(name, _admin);
        }

        protected void DisableProjection(string name)
        {
            _manager.Disable(name, _admin);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _conn.Close();
            _node.Shutdown();
            base.TestFixtureTearDown();
#if DEBUG
            QueueStatsCollector.InitializeIdleDetection(false);
#endif
        }

        protected virtual void When()
        {
        }

        protected virtual void Given()
        {
        }

        protected void PostEvent(string stream, string eventType, string data)
        {
            _conn.AppendToStream(stream, ExpectedVersion.Any, new[] {event_by_type_index.with_existing_events.CreateEvent(eventType, data)});
        }

        protected void HardDeleteStream(string stream)
        {
            _conn.DeleteStream(stream, ExpectedVersion.Any, true, _admin);
        }

        protected void SoftDeleteStream(string stream)
        {
            _conn.DeleteStream(stream, ExpectedVersion.Any, false, _admin);
        }

        protected static EventData CreateEvent(string type, string data)
        {
            return new EventData(Guid.NewGuid(), type, true, Encoding.UTF8.GetBytes(data), new byte[0]);
        }

        protected static void WaitIdle()
        {
            QueueStatsCollector.WaitIdle();
        }

        [Conditional("DEBUG")]
        protected void AssertStreamTail(string streamId, params string[] events)
        {
#if DEBUG
            var result = _conn.ReadStreamEventsBackward(streamId, -1, events.Length, true, _admin);
            switch (result.Status)
            {
                case SliceReadStatus.StreamDeleted:
                    Assert.Fail("Stream '{0}' is deleted", streamId);
                    break;
                case SliceReadStatus.StreamNotFound:
                    Assert.Fail("Stream '{0}' does not exist", streamId);
                    break;
                case SliceReadStatus.Success:
                    var resultEventsReversed = result.Events.Reverse().ToArray();
                    if (resultEventsReversed.Length < events.Length)
                        DumpFailed("Stream does not contain enough events", streamId, events, result.Events);
                    else
                    {
                        for (var index = 0; index < events.Length; index++)
                        {
                            var parts = events[index].Split(new char[] { ':' }, 2);
                            var eventType = parts[0];
                            var eventData = parts[1];

                            if (resultEventsReversed[index].Event.EventType != eventType)
                                DumpFailed("Invalid event type", streamId, events, resultEventsReversed);
                            else
                                if (resultEventsReversed[index].Event.DebugDataView != eventData)
                                    DumpFailed("Invalid event body", streamId, events, resultEventsReversed);
                        }
                    }
                    break;
            }
#endif
        }

        [Conditional("DEBUG")]
        protected void DumpStream(string streamId)
        {
#if DEBUG
            var result = _conn.ReadStreamEventsBackward(streamId, -1, 100, true, _admin);
            switch (result.Status)
            {
                case SliceReadStatus.StreamDeleted:
                    Assert.Fail("Stream '{0}' is deleted", streamId);
                    break;
                case SliceReadStatus.StreamNotFound:
                    Assert.Fail("Stream '{0}' does not exist", streamId);
                    break;
                case SliceReadStatus.Success:
                    Dump("Dumping..", streamId, result.Events.Reverse().ToArray());
                    break;
            }
#endif
        }

#if DEBUG
        private void DumpFailed(string message, string streamId, string[] events, ResolvedEvent[] resultEvents)
        {
            var expected = events.Aggregate("", (a, v) => a + ", " + v);
            var actual = resultEvents.Aggregate(
                "", (a, v) => a + ", " + v.Event.EventType + ":" + v.Event.DebugDataView);

            var actualMeta = resultEvents.Aggregate(
                "", (a, v) => a + "\r\n" + v.Event.EventType + ":" + v.Event.DebugMetadataView);


            Assert.Fail(
                "Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n Expected events: \r\n{3}\r\n\r\nActual metas:{4}", streamId,
                message, actual, expected, actualMeta);

        }

        private void Dump(string message, string streamId, ResolvedEvent[] resultEvents)
        {
            var actual = resultEvents.Aggregate(
                "", (a, v) => a + ", " + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugDataView);

            var actualMeta = resultEvents.Aggregate(
                "", (a, v) => a + "\r\n" + v.OriginalEvent.EventType + ":" + v.OriginalEvent.DebugMetadataView);


            Debug.WriteLine(
                "Stream: '{0}'\r\n{1}\r\n\r\nExisting events: \r\n{2}\r\n \r\nActual metas:{3}", streamId,
                message, actual, actualMeta);
        }
#endif

        protected void PostProjection(string query)
        {
            _manager.CreateContinuous("test-projection", query, _admin);
            WaitIdle();
        }
    }
}
