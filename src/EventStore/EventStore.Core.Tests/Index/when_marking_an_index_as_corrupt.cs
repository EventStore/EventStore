using System.IO;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_marking_an_index_as_corrupt: SpecificationWithDirectoryPerTestFixture
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            TableIndex.EnterUnsafeState(PathName);
        }

        [Test]
        public void the_file_exists()
        {
            Assert.IsTrue(File.Exists(GetFilePathFor("merging.m")));
        }

        [Test]
        public void the_map_says_its_in_corrupted_state()
        {
            Assert.IsTrue(TableIndex.IsCorrupt(PathName));
        }
    }
}