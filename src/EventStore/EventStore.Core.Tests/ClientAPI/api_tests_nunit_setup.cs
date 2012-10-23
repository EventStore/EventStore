using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [SetUpFixture]
    internal class api_tests_nunit_setup
    {
        [SetUp]
        public void SetUp()
        {
            MiniNode.Instance.Start();
        }

        [TearDown]
        public void TearDown()
        {
            MiniNode.Instance.Shutdown();
        }
    }
}
