using EventStore.Core.LogAbstraction;

namespace EventStore.Core.XUnit.Tests.LogAbstraction {
	public class MockExistenceFilterInitializer : INameExistenceFilterInitializer {
		private readonly string[] _names;

		public MockExistenceFilterInitializer(params string[] names) {
			_names = names;
		}

		public void Initialize(INameExistenceFilter filter) {
			int checkpoint = 0;
			foreach (var name in _names) {
				filter.Add(name);
				filter.CurrentCheckpoint = checkpoint++;
			}
		}
	}
}
