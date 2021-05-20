using EventStore.Core.Tests;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	class when_creating_a_multi_phase_projection<TLogFormat, TStreamId> : specification_with_multi_phase_core_projection<TLogFormat, TStreamId> {
		protected override void When() {
		}

		[Test]
		public void it_is_created() {
		}
	}
}

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase {
}
