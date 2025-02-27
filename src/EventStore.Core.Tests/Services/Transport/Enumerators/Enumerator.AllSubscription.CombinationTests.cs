// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.Core.Data;
using EventStore.Core.Services.Transport.Enumerators;
using EventStore.Core.Services.UserManagement;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using Position = EventStore.Core.Services.Transport.Common.Position;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;

namespace EventStore.Core.Tests.Services.Transport.Enumerators;

[TestFixture]
public partial class EnumeratorTests {
	[TestFixtureSource(nameof(TestCases))]
	public class AllSubscriptionCombinationTests : TestFixtureWithMiniNodeConnection {
		public record struct StreamProperties(int NumEvents = 0);
		public record struct SubscriptionProperties(CheckpointType CheckpointType = CheckpointType.Start);
		public record struct LiveProperties(int NumEventsToAdd = 0, bool RevokeAccessWithStreamAcl = false, bool RevokeAccessWithDefaultAcl = false, bool FallBehindThenCatchUp = false);
		public readonly record struct TestData(string TestCase, StreamProperties StreamProperties, SubscriptionProperties SubscriptionProperties, LiveProperties LiveProperties) {
			public override string ToString() {
				return TestCase;
			}
		}
		public static object[] CreateTestData(string testCase, StreamProperties streamProperties, SubscriptionProperties subscriptionProperties, LiveProperties liveProperties) {
			return new object[] {
				new TestData(testCase, streamProperties, subscriptionProperties, liveProperties)
			};
		}

		public enum CheckpointType {
			Start = 0,
			End = 1,
			AtZero = 2,
			OneBeforeLast = 3,
			AtLast = 4,
			OneByteAfterLast = 5,
			InvalidPosition = 6
		}

		private const int NumEventsToFallBehind = 3 * 32;

		public static object[] TestCases = {
			CreateTestData(
				"subscribe to $all from start",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.Start),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all from end",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.End),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all at position zero",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.AtZero),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all at one before last event",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.OneBeforeLast),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all at last event",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.AtLast),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all at one byte after last event",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.OneByteAfterLast),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all at an invalid position",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.InvalidPosition),
				new LiveProperties(3)
			),
			CreateTestData(
				"subscribe to $all from start then revoke access with stream acl",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.Start),
				new LiveProperties(RevokeAccessWithStreamAcl: true)
			),
			CreateTestData(
				"subscribe to $all from start then revoke access with default acl",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.Start),
				new LiveProperties(RevokeAccessWithDefaultAcl: true)
			),
			CreateTestData(
				"subscribe to $all from start then fall behind and catch up",
				new StreamProperties(10),
				new SubscriptionProperties(CheckpointType.Start),
				new LiveProperties(FallBehindThenCatchUp: true)
			),
		};

		private readonly TestData _testData;
		private StreamProperties StreamProps => _testData.StreamProperties;
		private SubscriptionProperties SubscriptionProps => _testData.SubscriptionProperties;
		private LiveProperties LiveProps => _testData.LiveProperties;

		private readonly string _streamPrefix;
		private int _streamSuffix;
		private List<ResolvedEvent> _events = new();

		private int _nextEventIndex;

		public AllSubscriptionCombinationTests(TestData testData) {
			_testData = testData;
			_streamPrefix = $"stream-{Guid.NewGuid()}-";
		}

		private Position GetPosition(ResolvedEvent @event) {
			var pos = @event.OriginalPosition!.Value;
			return Position.FromInt64(pos.CommitPosition, pos.PreparePosition);
		}

		private Position GetPositionPlusOneByte(ResolvedEvent @event) {
			var pos = GetPosition(@event);
			return new Position(pos.CommitPosition + 1, pos.PreparePosition + 1);
		}

		private Position GetPositionMinusOneByte(ResolvedEvent @event) {
			var pos = GetPosition(@event);
			return new Position(pos.CommitPosition - 1, pos.PreparePosition - 1);
		}

		private async Task WriteExistingEvents() {
			var numEvents = StreamProps.NumEvents;
			for (int i = 0; i < numEvents; i++)
				await WriteEvent();
		}

		private async Task PopulateExistingEvents() {
			_events.Clear();

			var result = await NodeConnection.ReadAllEventsForwardAsync(
				position: EventStore.ClientAPI.Position.Start,
				maxCount: 1000,
				resolveLinkTos: false);

			foreach (var @event in result.Events)
				_events.Add(@event);
		}

		private async Task WriteEvent(string stream, string eventType, string data, string metadata) {
			data ??= string.Empty;
			metadata ??= string.Empty;
			var eventData = new EventData(Guid.NewGuid(), eventType, true, Encoding.UTF8.GetBytes(data), Encoding.UTF8.GetBytes(metadata));
			await NodeConnection.AppendToStreamAsync(stream, ExpectedVersion.Any, eventData);
		}
		private Task WriteEvent() => WriteEvent(_streamPrefix + _streamSuffix++, "type", "{}", null);
		private Task RevokeAccessWithStreamAcl() => WriteEvent(SystemStreams.MetastreamOf(Core.Services.SystemStreams.AllStream), "$metadata", @"{ ""$acl"": { ""$r"": [] } }", null);
		private Task RevokeAccessWithDefaultAcl() => WriteEvent(SystemStreams.SettingsStream, "update-default-acl", @"{ ""$systemStreamAcl"" : { ""$r"" : [] } }", null);

		private async Task<(int numEventsAdded, bool accessRevoked, bool fallBehindThenCatchup)> ApplyLiveProperties() {
			var numEventsAdded = 0;
			var accessRevoked = false;
			var shouldFallBehindThenCatchup = false;

			if (LiveProps.NumEventsToAdd > 0) {
				numEventsAdded = LiveProps.NumEventsToAdd;
				for (var i = 0; i < numEventsAdded; i++)
					await WriteEvent();
			} else if (LiveProps.RevokeAccessWithStreamAcl) {
				numEventsAdded = 1;
				accessRevoked = true;
				await RevokeAccessWithStreamAcl();
			} else if (LiveProps.RevokeAccessWithDefaultAcl) {
				numEventsAdded = 1;
				accessRevoked = true;
				await RevokeAccessWithDefaultAcl();
			} else if (LiveProps.FallBehindThenCatchUp) {
				numEventsAdded = NumEventsToFallBehind;
				for (var i = 0; i < NumEventsToFallBehind; i++)
					await WriteEvent();

				shouldFallBehindThenCatchup = true;
			}

			// repopulate existing events
			await PopulateExistingEvents();

			return (numEventsAdded, accessRevoked, shouldFallBehindThenCatchup);
		}

		private int CalculateNextEventIndexFromCheckpoint() {
			return SubscriptionProps.CheckpointType switch {
				CheckpointType.Start => 0,
				CheckpointType.End => _events.Count,
				CheckpointType.AtZero => 0,
				CheckpointType.OneBeforeLast => _events.Count - 1,
				CheckpointType.AtLast => _events.Count,
				CheckpointType.OneByteAfterLast => _events.Count,
				CheckpointType.InvalidPosition => -1, // unused value as an exception should be thrown
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		private EnumeratorWrapper Subscribe() {
			Position? checkpoint = SubscriptionProps.CheckpointType switch {
				CheckpointType.Start => null,
				CheckpointType.End => Position.End,
				CheckpointType.AtZero => Position.Start,
				CheckpointType.OneBeforeLast => GetPosition(_events[^2]),
				CheckpointType.AtLast => GetPosition(_events[^1]),
				CheckpointType.OneByteAfterLast => GetPositionPlusOneByte(_events[^1]),
				CheckpointType.InvalidPosition => GetPositionMinusOneByte(_events[^1]),
				_ => throw new ArgumentOutOfRangeException()
			};

			return CreateAllSubscription(Node.Node.MainQueue, checkpoint, SystemAccounts.Anonymous);
		}

		private async Task<int> ReadExpectedEvents(EnumeratorWrapper sub, int nextEventIndex, int lastEventIndex, bool shouldFallBehindThenCatchUp = false) {
			var fellBehind = false;
			var caughtUp = false;

			var numResponsesExpected = lastEventIndex - nextEventIndex + 1;
			if (shouldFallBehindThenCatchUp)
				numResponsesExpected += 2;

			while (--numResponsesExpected >= 0) {
				var response = await sub.GetNext();
				switch (response) {
					case Event evt:
						var evtPos = _events[nextEventIndex++].OriginalPosition!.Value;
						var evtTfPos = new TFPos(evtPos.CommitPosition, evtPos.PreparePosition);
						Assert.AreEqual(evtTfPos, evt.EventPosition!.Value);
						break;
					case FellBehind:
						if (!shouldFallBehindThenCatchUp)
							Assert.Fail("Subscription fell behind.");

						fellBehind = true;
						break;
					case CaughtUp:
						if (!fellBehind)
							Assert.Fail("Subscription caught up before falling behind");

						if (!shouldFallBehindThenCatchUp)
							Assert.Fail("Subscription fell behind then caught up.");

						caughtUp = true;
						break;
					default:
						Assert.Fail($"Unexpected response: {response}");
						break ;
				}
			}

			if (shouldFallBehindThenCatchUp) {
				if (!fellBehind)
					Assert.Fail("Subscription did not fall behind.");

				if (!caughtUp)
					Assert.Fail("Subscription fell behind but did not catch up.");
			}

			return nextEventIndex;
		}

		[SetUp]
		public async Task SetUp() {
			await WriteExistingEvents();
			await PopulateExistingEvents();
			_nextEventIndex = CalculateNextEventIndexFromCheckpoint();
		}

		[Test]
		public async Task enumeration_is_correct() {
			var sub = Subscribe();

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);

			if (SubscriptionProps.CheckpointType == CheckpointType.InvalidPosition) {
				Assert.ThrowsAsync<ReadResponseException.InvalidPosition>(async () => await sub.GetNext());
				return;
			}

			_nextEventIndex = await ReadExpectedEvents(sub, _nextEventIndex, _events.Count - 1);

			Assert.True(await sub.GetNext() is CaughtUp);

			var (numEventsAdded, accessRevoked, shouldFallBehindThenCatchup) = await ApplyLiveProperties();

			_nextEventIndex = await ReadExpectedEvents(sub, _nextEventIndex, _nextEventIndex + numEventsAdded - 1, shouldFallBehindThenCatchup);

			if (accessRevoked) {
				Assert.ThrowsAsync<ReadResponseException.AccessDenied>(async () => await sub.GetNext());
			}
		}
	}
}
