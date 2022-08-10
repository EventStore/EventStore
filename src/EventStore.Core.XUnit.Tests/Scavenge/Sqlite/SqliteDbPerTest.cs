﻿using System.Threading.Tasks;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge.Sqlite {
	public class SqliteDbPerTest<T> : IAsyncLifetime {
		protected SqliteDbFixture<T> Fixture { get; }
		private DirectoryFixture<T> DirFixture { get; }

		public SqliteDbPerTest() {
			DirFixture = new DirectoryFixture<T>();
			Fixture = new SqliteDbFixture<T>(DirFixture.Directory);
		}
		
		public async Task InitializeAsync() {
			await DirFixture.InitializeAsync();
			await Fixture.InitializeAsync();
		}

		public async Task DisposeAsync() {
			await Fixture.DisposeAsync();
			await DirFixture.DisposeAsync();
		}
	}
}
