using System;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using EventStore.Core.Index;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Index.Performance
{
    [TestFixture, Explicit, Category("performance")]
    public class opening_and_reading_existing_index : SpecificationWithFilePerTestFixture
    {
        private PTable _ptable;
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
        }

        public override void TestFixtureTearDown()
        {
            _ptable.Dispose();
            base.TestFixtureTearDown();
        }

        [Test, Explicit, Category("performance")]
        public void opening_with_persisted_midpoints()
        {
            var watch = new Stopwatch();
            watch.Start();
            _ptable = PTable.FromFile(@"C:\databases\small_index\80822625-d7f8-4590-b247-dad73174f8fb-index_with_a_hundred_million_r", false, false, 16);
            watch.Stop();
            Console.WriteLine($"Opening the index took {watch.ElapsedMilliseconds} ms");
            watch.Reset();
            watch.Start();
            foreach (var indexEntry in _ptable.IterateAllInOrder())
            {

            }
            watch.Stop();
            Console.WriteLine($"Reading the index took {watch.ElapsedMilliseconds} ms");
        }

        [Test, Explicit, Category("performance")]
        public void opening_and_reading_normally()
        {
            var watch = new Stopwatch();
            watch.Start();
            File.Delete(@"C:\databases\small_index\80822625-d7f8-4590-b247-dad73174f8fb-index_with_a_hundred_million_r");
            _ptable = PTable.FromFile(@"C:\databases\large_index\8adaf744-3940-4cb7-a669-23ebf0d8fbc7-index_with_a_hundred_million_r", false, false, 16);
            watch.Stop();
            Console.WriteLine($"Opening the index took {watch.ElapsedMilliseconds} ms");
            watch.Reset();
            watch.Start();
            foreach(var indexEntry in _ptable.IterateAllInOrder())
            {

            }
            watch.Stop();
            Console.WriteLine($"Reading the index took {watch.ElapsedMilliseconds} ms");
        }

        [Test, Explicit, Category("performance")]
        public void opening_and_reading_using_memory_mapped_files()
        {
            var watch = new Stopwatch();
            watch.Start();
            _ptable = PTable.FromFile(@"C:\databases\large_index\8adaf744-3940-4cb7-a669-23ebf0d8fbc7-index_with_a_hundred_million_r", true, true, 16);
            watch.Stop();
            Console.WriteLine($"Opening the index took {watch.ElapsedMilliseconds} ms");
            watch.Reset();
            watch.Start();
            foreach(var indexEntry in _ptable.IterateAllInOrder())
            {

            }
            watch.Stop();
            Console.WriteLine($"Reading the index took {watch.ElapsedMilliseconds} ms");
        }
    }
}