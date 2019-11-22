using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventStore.ClientAPI.Tests;
using Xunit;

namespace EventStore.Sadness {
	internal class Program {
		public static async Task<int> Main(string[] args) {
			int exitCode = 0;
			await using var fixture = new EventStoreClientAPIFixture();

			await fixture.InitializeAsync();

			foreach (var testClass in typeof(EventStoreClientAPIFixture).Assembly.GetTypes().Where(IsClientAPITest)
				.Select(CreateTestClass)) {
				if (testClass is IAsyncLifetime initialize) {
					try {
						await initialize.InitializeAsync();
					} catch (Exception ex) {
						TestResultPrinter.FailInitialize(testClass.GetType().Name, ex);
						exitCode++;

						try {
							await initialize.DisposeAsync();
						} catch (Exception ex2) {
							TestResultPrinter.FailCleanup(testClass.GetType().Name, ex2);
						}
					}
				}

				foreach (var (name, testCase) in GetFacts(testClass)) {
					try {
						await testCase();
						TestResultPrinter.Pass(name);
					} catch (Exception ex) {
						TestResultPrinter.Fail(name, ex);
						exitCode++;
					}
				}

				if (testClass is IAsyncLifetime dispose) {
					try {
						await dispose.DisposeAsync();
					} catch (Exception ex) {
						TestResultPrinter.FailCleanup(testClass.GetType().Name, ex);
					}
				}

				if (testClass is IDisposable d) {
					try {
						d.Dispose();
					} catch (Exception ex) {
						TestResultPrinter.FailCleanup(testClass.GetType().Name, ex);
					}
				}
			}

			return exitCode;

			static bool IsClientAPITest(Type type) => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
				.Any(
					constructor => {
						var parameters = constructor.GetParameters();
						return parameters.Length == 1 &&
						       typeof(EventStoreClientAPIFixture).IsAssignableFrom(parameters[0].ParameterType);
					});

			object CreateTestClass(Type type) => Activator.CreateInstance(type, fixture);

			static IEnumerable<(string, Func<Task>)> GetFacts(object testClass) {
				foreach (var method in testClass.GetType().GetMethods()) {
					var theory = method.GetCustomAttribute<TheoryAttribute>();
					if (theory == null || theory.Skip != null) {
						continue;
					}

					foreach (var memberData in method.GetCustomAttributes<MemberDataAttribute>()) {
						foreach (var parameters in memberData.GetData(method)) {
							yield return ($"{testClass.GetType().Name}.{method.Name}",
								() => method.Invoke(testClass, parameters) as Task);
						}
					}
				}
			}
		}
	}
}
