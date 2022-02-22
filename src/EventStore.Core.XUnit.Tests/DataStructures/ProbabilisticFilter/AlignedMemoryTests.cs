using EventStore.Core.DataStructures.ProbabilisticFilter;
using Xunit;

namespace EventStore.Core.XUnit.Tests.DataStructures.ProbabilisticFilter {
	public unsafe class AlignedMemoryTests {
		[Theory]
		[InlineData(1, 1)]
		[InlineData(3, 8)]
		[InlineData(8, 3)]
		[InlineData(30_000, 8 * 1024)]
		[InlineData(int.MaxValue + 8L, 8 * 1024)]
		public void Works(long size, int alignTo) {
			using var sut = new AlignedMemory(size, alignTo);

			Assert.True((long)sut.Pointer % alignTo == 0);

			if (size <= int.MaxValue) {
				Assert.Equal(size, sut.AsSpan().Length);
				sut.AsSpan().Clear(); // can write to the span
			}
		}
	}
}
