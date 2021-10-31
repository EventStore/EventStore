using System;
using System.Collections.Generic;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	public abstract class INameExistenceFilterTests : IDisposable {
		private readonly List<IDisposable> _disposables = new();

		protected abstract INameExistenceFilter Sut { get; set; }
		protected virtual ILongHasher<string> Hasher { get; set; } =
			new CompositeHasher<string>(new XXHashUnsafe(), new Murmur3AUnsafe());

		protected void DisposeLater(IDisposable disposable) {
			_disposables.Add(disposable);
		}

		public void Dispose() {
			Sut?.Dispose();
			foreach (var disposable in _disposables)
				disposable.Dispose();
		}

		[Fact]
		public void can_initialize() {
			var names = new[] { "can_initialize" };
			var initializer = new MockExistenceFilterInitializer(names);
			Sut.Initialize(initializer, 0);

			foreach (var name in names)
				Assert.True(Sut.MightContain(name));
			Sut.Verify(corruptionThreshold: 0);
		}

		[Fact]
		public void can_add_name() {
			var name = "can_add_name";
			Sut.Add(name);
			Assert.True(Sut.MightContain(name));
			Sut.Verify(corruptionThreshold: 0);
		}

		[Fact]
		public void can_add_hash() {
			var name = "can_add_hash";
			Sut.Add(Hasher.Hash(name));
			Assert.True(Sut.MightContain(name));
			Sut.Verify(corruptionThreshold: 0);
		}

		[Fact]
		public void can_add_many() {
			for (int i = 0; i < 1000; i++)
				Sut.Add($"{i}");
			for (int i = 0; i < 1000; i++)
				Assert.True(Sut.MightContain($"{i}"));
			Sut.Verify(corruptionThreshold: 0);
		}

		[Fact]
		public void can_checkpoint() {
			Assert.Equal(-1L, Sut.CurrentCheckpoint);
			Sut.CurrentCheckpoint = 5;
			Assert.Equal(5, Sut.CurrentCheckpoint);
			Sut.Verify(corruptionThreshold: 0);
		}
	}
}
