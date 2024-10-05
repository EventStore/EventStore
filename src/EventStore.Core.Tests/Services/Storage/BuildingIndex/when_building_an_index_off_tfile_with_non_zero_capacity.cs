// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex {
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_building_an_index_off_tfile_with_non_zero_capacity<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		public when_building_an_index_off_tfile_with_non_zero_capacity() : base(streamInfoCacheCapacity: 20) {
		}

		protected override async ValueTask WriteTestScenario(CancellationToken token) {
			await GetOrReserve("test1", token);
			await GetOrReserve("test2", token);
			await GetOrReserve("test3", token);
		}

		[Test]
		public void the_stream_created_records_can_be_read() {
			var records = ReadIndex.ReadStreamEventsForward(SystemStreams.StreamsCreatedStream, 0, 20).Records;
			Assert.AreEqual(3, records.Length);
		}
	}
}
