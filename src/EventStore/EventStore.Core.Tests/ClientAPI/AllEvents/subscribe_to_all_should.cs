using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.AllEvents
{
    [TestFixture]
    internal class subscribe_to_all_should
    {
        public MiniNode Node;

        //[TestFixtureSetUp]
        public void SetUp()
        {
            Node = MiniNode.Create(8111, 9111);
            Node.Start();
        }

        //[TestFixtureTearDown]
        public void TearDown()
        {
            Node.Shutdown();
        }
    }
}
