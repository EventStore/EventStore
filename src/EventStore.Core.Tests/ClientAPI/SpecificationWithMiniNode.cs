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

        protected abstract void When();

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
            _node.Start();
            _conn = TestConnection.Create(_node.TcpEndPoint);
            _conn.ConnectAsync().Wait();
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
