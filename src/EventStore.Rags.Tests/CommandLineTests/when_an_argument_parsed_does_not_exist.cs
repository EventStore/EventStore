using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.CommandLineTests
{
    [TestFixture]
    public class when_an_argument_parsed_does_not_exist
    {
        [Test]
        public void should_return_no_results()
        {
            IEnumerable<OptionSource> result = CommandLine.Parse<TestType>(new[] { "--nonExistentName=bar" });
            Assert.AreEqual(0, result.Count());
        }
    }
}
