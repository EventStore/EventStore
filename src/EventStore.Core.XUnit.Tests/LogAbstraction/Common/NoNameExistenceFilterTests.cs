using EventStore.Core.LogAbstraction;
using EventStore.Core.LogAbstraction.Common;

namespace EventStore.Core.XUnit.Tests.LogAbstraction.Common {
	public class NoNameExistenceFilterTests : INameExistenceFilterTests {
		protected override INameExistenceFilter Sut { get; set; } =
			new NoNameExistenceFilter();
	}
}
