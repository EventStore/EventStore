using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests.EnvironmentTests {
	[TestFixture]
	public class when_referenced_environment_variable_is_parsed {
		[Test]
		public void should_return_the_referenced_environment_variable() {
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", "${env:TEST_REFERENCE_VAR}",
				EnvironmentVariableTarget.Process);
			Environment.SetEnvironmentVariable("TEST_REFERENCE_VAR", "foo", EnvironmentVariableTarget.Process);

			var envVariable = Environment.GetEnvironmentVariable("EVENTSTORE_NAME");
			var result =
				EnvironmentVariables.Parse<TestType>(x => NameTranslators.PrefixEnvironmentVariable(x, "EVENTSTORE_"));
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("Name", result.First().Name);
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual("foo", result.First().Value.ToString());
			Assert.AreEqual(true, result.First().IsReference);
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", null, EnvironmentVariableTarget.Process);
			Environment.SetEnvironmentVariable("TEST_REFERENCE_VAR", null, EnvironmentVariableTarget.Process);
		}

		[Test]
		public void should_return_null_if_referenced_environment_variable_does_not_exist() {
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", "${env:TEST_REFERENCE_VAR}",
				EnvironmentVariableTarget.Process);
			var envVariable = Environment.GetEnvironmentVariable("EVENTSTORE_NAME");
			var result =
				EnvironmentVariables.Parse<TestType>(x => NameTranslators.PrefixEnvironmentVariable(x, "EVENTSTORE_"));
			Assert.AreEqual(1, result.Count());
			Assert.AreEqual("Name", result.First().Name);
			Assert.AreEqual(false, result.First().IsTyped);
			Assert.AreEqual(null, result.First().Value);
			Assert.AreEqual(true, result.First().IsReference);
			Environment.SetEnvironmentVariable("EVENTSTORE_NAME", null, EnvironmentVariableTarget.Process);
		}
	}
}
