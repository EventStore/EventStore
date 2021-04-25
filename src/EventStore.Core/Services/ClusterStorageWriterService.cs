using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Messages;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services {
	public class ClusterStorageWriterService {
	}

	public class ClusterStorageWriterService<TStreamId> : StorageWriterService<TStreamId>,
		IHandle<ReplicationMessage.ReplicaSubscribed>,
		IHandle<ReplicationMessage.CreateChunk>,
		IHandle<ReplicationMessage.RawChunkBulk>,
		IHandle<ReplicationMessage.DataChunkBulk> {
		private static readonly ILogger Log = Serilog.Log.ForContext<ClusterStorageWriterService>();

		private readonly Func<long> _getLastIndexedPosition;
		private readonly LengthPrefixSuffixFramer _framer;

		private Guid _subscriptionId;
		private TFChunk _activeChunk;
		private long _subscriptionPos;
		private long _ackedSubscriptionPos;

		public ClusterStorageWriterService(IPublisher bus,
			ISubscriber subscribeToBus,
			TimeSpan minFlushDelay,
			TFChunkDb db,
			TFChunkWriter writer,
			IIndexWriter<TStreamId> indexWriter,
			IRecordFactory<TStreamId> recordFactory,
			IStreamNameIndex<TStreamId> streamNameIndex,
			ISystemStreamLookup<TStreamId> systemStreams,
			IEpochManager epochManager,
			QueueStatsManager queueStatsManager,
			Func<long> getLastIndexedPosition)
			: base(bus, subscribeToBus, minFlushDelay, db, writer, indexWriter, recordFactory, streamNameIndex, systemStreams, epochManager, queueStatsManager) {
			Ensure.NotNull(getLastIndexedPosition, "getLastCommitPosition");

			_getLastIndexedPosition = getLastIndexedPosition;
			_framer = new LengthPrefixSuffixFramer(OnLogRecordUnframed, TFConsts.MaxLogRecordSize);

			SubscribeToMessage<ReplicationMessage.ReplicaSubscribed>();
			SubscribeToMessage<ReplicationMessage.CreateChunk>();
			SubscribeToMessage<ReplicationMessage.RawChunkBulk>();
			SubscribeToMessage<ReplicationMessage.DataChunkBulk>();
		}

		public override void Handle(SystemMessage.StateChangeMessage message) {
			if (message.State == VNodeState.PreLeader) {
				if (_activeChunk != null) {
					_activeChunk.MarkForDeletion();
					_activeChunk = null;
				}

				_subscriptionId = Guid.Empty;
				_subscriptionPos = -1;
				_ackedSubscriptionPos = -1;
			}

			base.Handle(message);
		}

		public void Handle(ReplicationMessage.ReplicaSubscribed message) {
			if (_activeChunk != null) {
				_activeChunk.MarkForDeletion();
				_activeChunk = null;
			}

			_framer.Reset();

			_subscriptionId = message.SubscriptionId;
			_ackedSubscriptionPos = _subscriptionPos = message.SubscriptionPosition;

			Log.Information(
				"=== SUBSCRIBED to [{leaderEndPoint},{leaderId:B}] at {subscriptionPosition} (0x{subscriptionPosition:X}). SubscriptionId: {subscriptionId:B}.",
				message.LeaderEndPoint, message.LeaderId, message.SubscriptionPosition, message.SubscriptionPosition,
				message.SubscriptionId);

			var writerCheck = Db.Config.WriterCheckpoint.ReadNonFlushed();
			if (message.SubscriptionPosition > writerCheck) {
				ReplicationFail(
					"Leader [{0},{1:B}] subscribed us at {2} (0x{3:X}), which is greater than our writer checkpoint {4} (0x{5:X}). REPLICATION BUG.",
					"Leader [{leaderEndpoint},{leaderId:B}] subscribed us at {subscriptionPosition} (0x{subscriptionPosition:X}), which is greater than our writer checkpoint {writerCheckpoint} (0x{writerCheckpoint:X}). REPLICATION BUG.",
					message.LeaderEndPoint, message.LeaderId, message.SubscriptionPosition,
					message.SubscriptionPosition, writerCheck, writerCheck);
			}

			if (message.SubscriptionPosition < writerCheck) {
				Log.Information(
					"Leader [{leaderEndPoint},{leaderId:B}] subscribed us at {subscriptionPosition} (0x{subscriptionPosition:X}), which is less than our writer checkpoint {writerCheckpoint} (0x{writerCheckpoint:X}). TRUNCATION IS NEEDED.",
					message.LeaderEndPoint, message.LeaderId, message.SubscriptionPosition,
					message.SubscriptionPosition, writerCheck, writerCheck);

				var lastIndexedPosition = _getLastIndexedPosition();
				if (message.SubscriptionPosition > lastIndexedPosition)
					Log.Information(
						"ONLINE TRUNCATION IS NEEDED. NOT IMPLEMENTED. OFFLINE TRUNCATION WILL BE PERFORMED. SHUTTING DOWN NODE.");
				else
					Log.Information(
						"OFFLINE TRUNCATION IS NEEDED (SubscribedAt {subscriptionPosition} (0x{subscriptionPosition:X}) <= LastCommitPosition {lastCommitPosition} (0x{lastCommitPosition:X})). SHUTTING DOWN NODE.",
						message.SubscriptionPosition, message.SubscriptionPosition, lastIndexedPosition,
						lastIndexedPosition);

				EpochRecord lastEpoch = EpochManager.GetLastEpoch();
				if (AreAnyCommittedRecordsTruncatedWithLastEpoch(message.SubscriptionPosition, lastEpoch,
					lastIndexedPosition)) {
					Log.Error(
						"Leader [{leaderEndPoint},{leaderId:B}] subscribed us at {subscriptionPosition} (0x{subscriptionPosition:X}), which is less than our last epoch and LastCommitPosition {lastCommitPosition} (0x{lastCommitPosition:X}) >= lastEpoch.EpochPosition {lastEpochPosition} (0x{lastEpochPosition:X}). That might be bad, especially if the LastCommitPosition is way beyond EpochPosition.",
						message.LeaderEndPoint, message.LeaderId, message.SubscriptionPosition,
						message.SubscriptionPosition, lastIndexedPosition, lastIndexedPosition, lastEpoch.EpochPosition,
						lastEpoch.EpochPosition);
					Log.Error(
						"ATTEMPT TO TRUNCATE EPOCH WITH COMMITTED RECORDS. THIS MAY BE BAD, BUT IT IS OK IF A NEWLY-ELECTED LEADER FAILS IMMEDIATELY AFTER ELECTION.");
				}

				Db.Config.TruncateCheckpoint.Write(message.SubscriptionPosition);
				Db.Config.TruncateCheckpoint.Flush();

				BlockWriter = true;
				Bus.Publish(new ClientMessage.RequestShutdown(exitProcess: true, shutdownHttp: true));
				return;
			}

			// subscription position == writer checkpoint
			// everything is ok
		}

		private bool AreAnyCommittedRecordsTruncatedWithLastEpoch(long subscriptionPosition, EpochRecord lastEpoch,
			long lastCommitPosition) {
			return lastEpoch != null && subscriptionPosition <= lastEpoch.EpochPosition &&
			       lastCommitPosition >= lastEpoch.EpochPosition;
		}

		public void Handle(ReplicationMessage.CreateChunk message) {
			if (_subscriptionId != message.SubscriptionId) return;

			if (_activeChunk != null) {
				_activeChunk.MarkForDeletion();
				_activeChunk = null;
			}

			_framer.Reset();

			if (message.IsCompletedChunk) {
				_activeChunk = Db.Manager.CreateTempChunk(message.ChunkHeader, message.FileSize);
			} else {
				if (message.ChunkHeader.ChunkStartNumber != Db.Manager.ChunksCount) {
					ReplicationFail(
						"Received request to create a new ongoing chunk #{0}-{1}, but current chunks count is {2}.",
						"Received request to create a new ongoing chunk #{chunkStartNumber}-{chunkEndNumber}, but current chunks count is {chunksCount}.",
						message.ChunkHeader.ChunkStartNumber, message.ChunkHeader.ChunkEndNumber,
						Db.Manager.ChunksCount);
				}

				Db.Manager.AddNewChunk(message.ChunkHeader, message.FileSize);
			}

			_subscriptionPos = message.ChunkHeader.ChunkStartPosition;
			_ackedSubscriptionPos = _subscriptionPos;
			Bus.Publish(new ReplicationMessage.AckLogPosition(_subscriptionId, _ackedSubscriptionPos));
		}

		public void Handle(ReplicationMessage.RawChunkBulk message) {
			if (_subscriptionId != message.SubscriptionId) return;
			if (_activeChunk == null)
				ReplicationFail(
					"Physical chunk bulk received, but we do not have active chunk.",
					"Physical chunk bulk received, but we do not have active chunk.");

			if (_activeChunk.ChunkHeader.ChunkStartNumber != message.ChunkStartNumber ||
			    _activeChunk.ChunkHeader.ChunkEndNumber != message.ChunkEndNumber) {
				Log.Error(
					"Received RawChunkBulk for TFChunk {chunkStartNumber}-{chunkEndNumber}, but active chunk is {activeChunk}.",
					message.ChunkStartNumber, message.ChunkEndNumber, _activeChunk);
				return;
			}

			if (_activeChunk.RawWriterPosition != message.RawPosition) {
				Log.Error(
					"Received RawChunkBulk at raw pos {rawPosition} (0x{rawPosition:X}) while current writer raw pos is {rawWriterPosition} (0x{rawWriterPosition:X}).",
					message.RawPosition, message.RawPosition, _activeChunk.RawWriterPosition,
					_activeChunk.RawWriterPosition);
				return;
			}

			if (!_activeChunk.TryAppendRawData(message.RawBytes)) {
				ReplicationFail(
					"Could not append raw bytes to chunk {0}-{1}, raw pos: {2} (0x{3:X}), bytes length: {4} (0x{5:X}). Chunk file size: {6} (0x{7:X}).",
					"Could not append raw bytes to chunk {chunkStartNumber}-{chunkEndNumber}, raw pos: {rawPosition} (0x{rawPosition:X}), bytes length: {rawBytesLength} (0x{rawBytesLength:X}). Chunk file size: {chunkFileSize} (0x{chunkFileSize:X}).",
					message.ChunkStartNumber, message.ChunkEndNumber, message.RawPosition, message.RawPosition,
					message.RawBytes.Length, message.RawBytes.Length, _activeChunk.FileSize, _activeChunk.FileSize);
			}

			_subscriptionPos += message.RawBytes.Length;

			if (message.CompleteChunk) {
				Log.Debug("Completing raw chunk {chunkStartNumber}-{chunkEndNumber}...", message.ChunkStartNumber,
					message.ChunkEndNumber);
				Writer.CompleteReplicatedRawChunk(_activeChunk);

				_subscriptionPos = _activeChunk.ChunkHeader.ChunkEndPosition;
				_framer.Reset();
				_activeChunk = null;
			}

			if (message.CompleteChunk || _subscriptionPos > _ackedSubscriptionPos) {
				_ackedSubscriptionPos = _subscriptionPos;
				Bus.Publish(new ReplicationMessage.AckLogPosition(_subscriptionId, _ackedSubscriptionPos));
			}
		}

		public void Handle(ReplicationMessage.DataChunkBulk message) {
			Interlocked.Decrement(ref FlushMessagesInQueue);
			try {
				if (_subscriptionId != message.SubscriptionId) return;
				if (_activeChunk != null)
					ReplicationFail(
						"Data chunk bulk received, but we have active chunk for receiving raw chunk bulks.",
						"Data chunk bulk received, but we have active chunk for receiving raw chunk bulks.");

				var chunk = Writer.CurrentChunk;
				if (chunk.ChunkHeader.ChunkStartNumber != message.ChunkStartNumber ||
				    chunk.ChunkHeader.ChunkEndNumber != message.ChunkEndNumber) {
					Log.Error(
						"Received DataChunkBulk for TFChunk {chunkStartNumber}-{chunkEndNumber}, but active chunk is {activeChunkStartNumber}-{activeChunkEndNumber}.",
						message.ChunkStartNumber, message.ChunkEndNumber, chunk.ChunkHeader.ChunkStartNumber,
						chunk.ChunkHeader.ChunkEndNumber);
					return;
				}

				if (_subscriptionPos != message.SubscriptionPosition) {
					Log.Error(
						"Received DataChunkBulk at SubscriptionPosition {subscriptionPosition} (0x{subscriptionPosition:X}) while current SubscriptionPosition is {subscriptionPos} (0x{subscriptionPos:X}).",
						message.SubscriptionPosition, message.SubscriptionPosition, _subscriptionPos, _subscriptionPos);
					return;
				}

				_framer.UnFrameData(new ArraySegment<byte>(message.DataBytes));
				_subscriptionPos += message.DataBytes.Length;

				if (message.CompleteChunk) {
					Log.Debug("Completing data chunk {chunkStartNumber}-{chunkEndNumber}...", message.ChunkStartNumber,
						message.ChunkEndNumber);
					Writer.CompleteChunk();

					if (_framer.HasData)
						ReplicationFail(
							"There is some data left in framer when completing chunk.",
							"There is some data left in framer when completing chunk.");

					_subscriptionPos = chunk.ChunkHeader.ChunkEndPosition;
					_framer.Reset();
				}
			} catch (Exception exc) {
				Log.Error(exc, "Exception in writer.");
				throw;
			} finally {
				Flush();
			}

			if (message.CompleteChunk || _subscriptionPos > _ackedSubscriptionPos) {
				_ackedSubscriptionPos = _subscriptionPos;
				Bus.Publish(new ReplicationMessage.AckLogPosition(_subscriptionId, _ackedSubscriptionPos));
			}
		}

		private void OnLogRecordUnframed(BinaryReader reader) {
			var rawLength = reader.BaseStream.Length;

			if (rawLength >= int.MaxValue)
				throw new ArgumentOutOfRangeException(
					nameof(reader),
					$"Length of stream was {rawLength}");

			var length = (int)rawLength;

			var record = LogRecord.ReadFrom(reader, length: length);
			long newPos;
			if (!Writer.Write(record, out newPos))
				ReplicationFail(
					"First write failed when writing replicated record: {0}.",
					"First write failed when writing replicated record: {record}.",
					record);
		}

		private void ReplicationFail(string message, string messageStructured, params object[] args) {
			if (args.Length == 0) {
				Log.Fatal(messageStructured);
			} else {
				Log.Fatal(messageStructured, args);
			}

			var msg = args.Length == 0 ? message : string.Format(message, args);
			BlockWriter = true;
			Application.Exit(ExitCode.Error, msg);
			throw new Exception(msg);
		}
	}
}
