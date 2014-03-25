using System;
using System.IO;
using NUnit.Framework;

namespace EventStore.Core.Tests
{
    public class SpecificationWithFilePerTestFixture
    {
        protected string Filename;

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
            Filename = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", Guid.NewGuid(), typeName));
        }

        [TestFixtureTearDown]
        public virtual void TestFixtureTearDown()
        {
            if (File.Exists(Filename))
                File.Delete(Filename);
        }
    }
}