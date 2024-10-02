// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Transforms.Identity;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Optimization {
	[TestFixture]
	public class tfchunkreader_existsat_optimizer_should : SpecificationWithDirectoryPerTestFixture {
		[Test]
		public void have_a_single_instance() {
			var instance1 = TFChunkReaderExistsAtOptimizer.Instance;
			var instance2 = TFChunkReaderExistsAtOptimizer.Instance;
			Assert.AreEqual(instance1, instance2);
		}

		[Test]
		public async Task optimize_only_maxcached_items_at_a_time() {
			int maxCached = 3;
			List<TFChunk> chunks = new List<TFChunk>();
			TFChunkReaderExistsAtOptimizer _existsAtOptimizer = new TFChunkReaderExistsAtOptimizer(maxCached);

			for (int i = 0; i < 7; i++) {
				var chunk = await CreateChunk(i, true);
				chunks.Add(chunk);
				Assert.IsFalse(_existsAtOptimizer.IsOptimized(chunk));
				_existsAtOptimizer.Optimize(chunk);
			}


			//only the last maxCached chunks should still be optimized
			int cached = maxCached;
			for (int i = 7 - 1; i >= 0; i--) {
				if (cached > 0) {
					Assert.AreEqual(true, _existsAtOptimizer.IsOptimized(chunks[i]));
					cached--;
				} else {
					Assert.AreEqual(false, _existsAtOptimizer.IsOptimized(chunks[i]));
				}
			}

			foreach (var chunk in chunks) {
				chunk.MarkForDeletion();
				chunk.WaitForDestroy(5000);
			}
		}

		[Test]
		public async Task optimize_only_scavenged_chunks() {
			TFChunkReaderExistsAtOptimizer _existsAtOptimizer = new TFChunkReaderExistsAtOptimizer(3);
			var chunk = await CreateChunk(0, false);
			_existsAtOptimizer.Optimize(chunk);
			Assert.AreEqual(false, _existsAtOptimizer.IsOptimized(chunk));

			chunk.MarkForDeletion();
			chunk.WaitForDestroy(5000);
		}

		[Test]
		public async Task posmap_items_should_exist_in_chunk() {
			TFChunkReaderExistsAtOptimizer _existsAtOptimizer = new TFChunkReaderExistsAtOptimizer(3);
			List<PosMap> posmap = new();
			var chunk = await CreateChunk(0, true, posmap);

			//before optimization
			Assert.AreEqual(false, _existsAtOptimizer.IsOptimized(chunk));
			foreach (var p in posmap) {
				Assert.AreEqual(true, chunk.ExistsAt(p.LogPos));
			}

			//after optimization
			_existsAtOptimizer.Optimize(chunk);
			Assert.AreEqual(true, _existsAtOptimizer.IsOptimized(chunk));
			foreach (var p in posmap) {
				Assert.AreEqual(true, chunk.ExistsAt(p.LogPos));
			}

			chunk.MarkForDeletion();
			chunk.WaitForDestroy(5000);
		}

		private ValueTask<TFChunk> CreateChunk(int chunkNumber, bool scavenged) {
			List<PosMap> posmap = new();
			return CreateChunk(chunkNumber, scavenged, posmap);
		}

		private async ValueTask<TFChunk> CreateChunk(int chunkNumber, bool scavenged, List<PosMap> posmap) {
			var map = new List<PosMap>();
			var chunk = TFChunk.CreateNew(GetFilePathFor("chunk-" + chunkNumber + "-" + Guid.NewGuid()), 1024 * 1024,
				chunkNumber, chunkNumber, scavenged, false, false, false,
				false,
				new TFChunkTracker.NoOp(),
				new IdentityChunkTransformFactory());
			long offset = chunkNumber * 1024 * 1024;
			long logPos = 0 + offset;
			for (int i = 0, n = ChunkFooter.Size / PosMap.FullSize + 1; i < n; ++i) {
				if (scavenged)
					map.Add(new PosMap(logPos, (int)logPos));

				var res = chunk.TryAppend(LogRecord.Commit(logPos, Guid.NewGuid(), logPos, 0));
				Assert.IsTrue(res.Success);
				logPos = res.NewPosition + offset;
			}

			if (scavenged) {
				posmap.AddRange(map);
				await chunk.CompleteScavenge(map, CancellationToken.None);
			} else {
				posmap.Clear();
				chunk.Complete();
			}

			return chunk;
		}
	}
}
