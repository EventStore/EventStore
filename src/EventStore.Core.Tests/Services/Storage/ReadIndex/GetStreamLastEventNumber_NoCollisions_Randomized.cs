// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Tests.Index.Hashers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.ReadIndex {
	[TestFixture(3)]
	[TestFixture(33)]
	[TestFixture(123)]
	[TestFixture(523)]
	public class GetStreamLastEventNumber_NoCollisions_Randomized : ReadIndexTestScenario<LogFormat.V2, string> {
		private const string Stream = "ab-1";
		private const ulong Hash = 98;
		private const string NonCollidingStream = "cd-1";
		private string GetStreamId(ulong hash) => hash == Hash ? Stream : throw new ArgumentException();

		private readonly Random _random = new Random();
		private readonly int _numEvents;
		private readonly List<EventRecord> _events;

		public GetStreamLastEventNumber_NoCollisions_Randomized(int maxEntriesInMemTable) : base(
			chunkSize: 1_000_000,
			maxEntriesInMemTable: maxEntriesInMemTable,
			lowHasher: new ConstantHasher(0),
			highHasher: new HumanReadableHasher32()) {
			_numEvents = _random.Next(100, 400);
			_events = new List<EventRecord>(_numEvents);
		}

		protected override async ValueTask WriteTestScenario(CancellationToken token) {
			var streamLast = 0L;
			var nonCollidingStreamLast = 0L;

			for (int i = 0; i < _numEvents; i++) {
				if (_random.Next(2) == 0) {
					_events.Add(await WriteSingleEvent(Stream, streamLast++, "test data", token: token));
				} else {
					_events.Add(await WriteSingleEvent(NonCollidingStream, nonCollidingStreamLast++, "testing", token: token));
				}
			}
		}

		[Test]
		public void returns_correct_last_event_number_before_position() {
			var expectedLastEventNumber = ExpectedVersion.NoStream;

			foreach (var @event in _events)
			{
				Assert.AreEqual(expectedLastEventNumber,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(Hash, GetStreamId, @event.LogPosition));

				if (@event.EventStreamId == Stream)
					expectedLastEventNumber = @event.EventNumber;
			}
		}
	}

}
