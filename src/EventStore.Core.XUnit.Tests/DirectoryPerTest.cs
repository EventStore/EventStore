using System.Threading.Tasks;
using Xunit;

namespace EventStore.Core.XUnit.Tests {
	public class DirectoryPerTest<T> : IAsyncLifetime {
		protected DirectoryFixture<T> Fixture { get; }

		public DirectoryPerTest() {
			Fixture = new DirectoryFixture<T>();
		}

		public async Task InitializeAsync() {
			await Fixture.InitializeAsync();
		}

		public async Task DisposeAsync() {
			await Fixture.DisposeAsync();
		}
	}
}
