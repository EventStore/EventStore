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

        protected virtual IEventStoreConnection BuildConnection(MiniNode node)
        {
            return TestConnection.Create(node.TcpEndPoint);
        }

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName, skipInitializeStandardUsersCheck: false);
            _node.Start();
            _conn = BuildConnection(_node);
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
