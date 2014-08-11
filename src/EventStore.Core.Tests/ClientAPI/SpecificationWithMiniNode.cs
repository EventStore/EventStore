using System.Net;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    public abstract class SpecificationWithMiniNode : SpecificationWithDirectoryPerTestFixture
    {
        private MiniNode _node;
        protected IEventStoreConnection _conn;
        protected IPEndPoint _HttpEndPoint;

        protected virtual void Given()
        {
        }

        protected abstract void When();

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
            _node.Start();
            _HttpEndPoint = _node.HttpEndPoint;
            _conn = TestConnection.Create(_node.TcpEndPoint);
            _conn.ConnectAsync().Wait();
            Given();
            When();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _conn.Close();
            _node.Shutdown();
            base.TestFixtureTearDown();
        }
    }
}
