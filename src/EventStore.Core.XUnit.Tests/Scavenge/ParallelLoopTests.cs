﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Core.TransactionLog.Scavenging;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class ParallelLoopTests {
		void Run(
			int[] source,
			int[] completionOrder,
			int[] expectedCheckpoints,
			int degreeOfParallelism = 2) {

			if (source.Any(x => x % 10 != 0))
				throw new Exception("use multiples of 10");

			// maps from the item to the mres it should wait for
			var selfTriggers = new Dictionary<int, ManualResetEventSlim>();
			// maps from item to the mres it should trigger when it completes
			var nextTriggers = new Dictionary<int, ManualResetEventSlim>();

			var prev = default(int?);
			foreach (var item in completionOrder) {
				selfTriggers[item] = new ManualResetEventSlim();
				if (prev != null)
					nextTriggers[prev.Value] = selfTriggers[item];

				prev = item;
			}

			if (completionOrder.Length > 0) {
				nextTriggers[completionOrder.Last()] = new ManualResetEventSlim();
				selfTriggers[completionOrder[0]].Set();
			}

			// make sure only one item completes at a time, to force the sut to always pick the same slot
			var serializer = new ManualResetEventSlim(true);
			var emittedCheckpoints = new List<int>();
			var completedItems = new List<int>();

			var loopThread = Thread.CurrentThread.ManagedThreadId;
			ParallelLoop.RunWithTrailingCheckpoint(
				source: source,
				degreeOfParallelism: degreeOfParallelism,
				getCheckpointInclusive: x => x,
				getCheckpointExclusive: x => {
					var chunkStartNumber = x == 10
						? x - 10
						: x - 9;
					if (chunkStartNumber == 00)
						return null;
					return chunkStartNumber - 1;
				},
				process: (slot, x) => {
					Assert.NotEqual(loopThread, Thread.CurrentThread.ManagedThreadId);
					// wait until we are complete
					selfTriggers[x].Wait();
					completedItems.Add(x);

					serializer.Wait();
					serializer.Reset();

					// complete the next in line
					nextTriggers[x].Set();
				},
				emitCheckpoint: checkpoint => {
					Assert.Equal(loopThread, Thread.CurrentThread.ManagedThreadId);
					emittedCheckpoints.Add(checkpoint);
				},
				onConsiderEmit: () => serializer.Set());


			Assert.Equal(completionOrder, completedItems);
			Assert.Equal(expectedCheckpoints, emittedCheckpoints);
		}

		[Fact]
		public void process_out_of_order() => Run(
			source: new int[] { 10, 20, 30, 40, 50 },
			completionOrder: new int[] { 20, 30, 40, 10, 50 },
			expectedCheckpoints: new int[] { 40, 50 });

		[Fact]
		public void process_very_out_of_order() => Run(
			source: new int[] { 10, 20, 30, 40, 50 },
			completionOrder: new int[] { 20, 30, 40, 50, 10 },
			expectedCheckpoints: new int[] { 50 });

		[Fact]
		public void process_interleaved() => Run(
			source: new int[] { 10, 20, 30, 40, 50 },
			completionOrder: new int[] { 20, 10, 30, 40, 50 },
			expectedCheckpoints: new int[] { 20, 30, 40, 50 });

		[Fact]
		public void one_degree_of_parallelism() => Run(
			source: new int[] { 10, 20, 30 },
			completionOrder: new int[] { 10, 20, 30 },
			expectedCheckpoints: new int[] { 10, 20, 30 },
			degreeOfParallelism: 1);

		[Fact]
		public void four_degrees_of_parallelism() => Run(
			source: new int[] { 10, 20, 30, 40, 50 },
			completionOrder: new int[] { 10, 20, 30, 40, 50 },
			expectedCheckpoints: new int[] { 10, 20, 30, 40, 50 },
			degreeOfParallelism: 4);

		[Fact]
		public void same_degress_as_elements() => Run(
			source: new int[] { 10, 20 },
			completionOrder: new int[] { 10, 20 },
			expectedCheckpoints: new int[] { 10, 20 });

		[Fact]
		public void same_degress_as_elements_out_of_order() => Run(
			source: new int[] { 10, 20 },
			completionOrder: new int[] { 20, 10 },
			expectedCheckpoints: new int[] { 20 });


		[Fact]
		public void more_degress_than_elements() => Run(
			source: new int[] { 10 },
			completionOrder: new int[] { 10 },
			expectedCheckpoints: new int[] { 10 });

		[Fact]
		public void empty_source() => Run(
			source: new int[] { },
			completionOrder: new int[] { },
			expectedCheckpoints: new int[] { });

		[Fact]
		public void exception_during_processing_is_propagated() {
			var ex = Assert.Throws<InvalidOperationException>(() => {
				ParallelLoop.RunWithTrailingCheckpoint(
					source: new int[] { 10 },
					degreeOfParallelism: 2,
					getCheckpointInclusive: x => x,
					getCheckpointExclusive: x => x,
					process: (slot, x) => {
						throw new InvalidOperationException("something went wrong");
					},
					emitCheckpoint: checkpoint => {
					});
			});

			Assert.Equal("something went wrong", ex.Message);
		}

		[Fact]
		public void exception_during_emit_is_propagated() {
			var ex = Assert.Throws<InvalidOperationException>(() => {
				ParallelLoop.RunWithTrailingCheckpoint(
					source: new int[] { 10 },
					degreeOfParallelism: 2,
					getCheckpointInclusive: x => x,
					getCheckpointExclusive: x => x,
					process: (slot, x) => {
					},
					emitCheckpoint: checkpoint => {
						throw new InvalidOperationException("something went wrong");
					});
			});

			Assert.Equal("something went wrong", ex.Message);
		}
	}
}
