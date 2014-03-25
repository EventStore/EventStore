using System.IO;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_marking_an_index_map_as_corrupt_then_ok: SpecificationWithDirectoryPerTestFixture
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            TableIndex.EnterUnsafeState(PathName);
            TableIndex.LeaveUnsafeState(PathName);
        }

        [Test]
        public void the_file_does_not_exist()
        {
            Assert.IsFalse(File.Exists(GetFilePathFor("merging.m")));
        }

        [Test]
        public void the_map_says_its_not_in_corrupted_state()
        {
            Assert.IsFalse(TableIndex.IsCorrupt(PathName));
        }
    }
}