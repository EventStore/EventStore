using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.EnvironmentTests {
	[TestFixture]
	public class when_environment_variable_is_parsed {
		[Test]
		public void should_return_the_environment_variables() {
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", "foo", EnvironmentVariableTarget.Process);
			var envVariable = Environment.GetEnvironmentVariable("EVENTSTORE_NAME");
			var result =
				EnvironmentVariables.Parse<TestType>(x => NameTranslators.PrefixEnvironmentVariable(x, "EVENTSTORE_"));
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("Name", result.First().Name);
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("foo", result.First().Value.ToString());
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", null, EnvironmentVariableTarget.Process);
		}
	}
}
