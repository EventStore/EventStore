using System;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Chunks;

public class ChunkFooterTests {
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void can_round_trip(bool isCompleted, bool isMap12Bytes) {
		var hash = new byte[ChunkFooter.ChecksumSize];
		Random.Shared.NextBytes(hash);

		var source = new ChunkFooter(
			isCompleted: isCompleted,
			isMap12Bytes: isMap12Bytes,
			physicalDataSize: Random.Shared.Next(500, 600),
			logicalDataSize: Random.Shared.Next(600, 700),
			mapSize: Random.Shared.Next(500, 600).RoundUpToMultipleOf(24),
			md5Hash: hash);

		var destination = new ChunkFooter(source.AsByteArray());

		Assert.Equal(source.IsCompleted, destination.IsCompleted);
		Assert.Equal(source.IsMap12Bytes, destination.IsMap12Bytes);
		Assert.Equal(source.PhysicalDataSize, destination.PhysicalDataSize);
		Assert.Equal(source.LogicalDataSize, destination.LogicalDataSize);
		Assert.Equal(source.MapSize, destination.MapSize);
		Assert.Equal(source.MD5Hash, destination.MD5Hash);
	}
}
