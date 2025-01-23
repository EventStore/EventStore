// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.LogV3;

public class PartitionManager(ITransactionFileReader reader, ITransactionFileWriter writer, LogV3RecordFactory recordFactory) : IPartitionManager {
	private static readonly ILogger _log = Serilog.Log.ForContext<PartitionManager>();

	private const string RootPartitionName = "Root";
	private const string RootPartitionTypeName = "Root";

	public Guid? RootId { get; private set; }
	public Guid? RootTypeId  { get; private set; }

	public async ValueTask Initialize(CancellationToken token) {
		if (RootId.HasValue)
			return;

		await ReadRootPartition(token);
		await EnsureRootPartitionIsWritten(token);
	}

	private async ValueTask EnsureRootPartitionIsWritten(CancellationToken token) {
		// below code only takes into account offline truncation
		if (!RootTypeId.HasValue) {
			RootTypeId = Guid.NewGuid();
			long pos = writer.Position;
			var rootPartitionType = recordFactory.CreatePartitionTypeRecord(
				timeStamp: DateTime.UtcNow,
				logPosition: pos,
				partitionTypeId: RootTypeId.Value,
				partitionId: Guid.Empty,
				name: RootPartitionTypeName);

			if (await writer.Write(rootPartitionType, token) is (false, _))
				throw new Exception($"Failed to write root partition type!");

			await writer.Flush(token);

			_log.Debug("Root partition type created, id: {id}", RootTypeId);
		}

		if (!RootId.HasValue) {
			RootId = Guid.NewGuid();
			long pos = writer.Position;
			var rootPartition = recordFactory.CreatePartitionRecord(
				timeStamp: DateTime.UtcNow,
				logPosition: pos,
				partitionId: RootId.Value,
				partitionTypeId: RootTypeId.Value,
				parentPartitionId: Guid.Empty,
				flags: 0,
				referenceNumber: 0,
				name: RootPartitionName);

			if (await writer.Write(rootPartition, token) is (false, _))
				throw new Exception($"Failed to write root partition!");

			await writer.Flush(token);

			recordFactory.SetRootPartitionId(RootId.Value);

			_log.Debug("Root partition created, id: {id}", RootId);
		}
	}

	private async ValueTask ReadRootPartition(CancellationToken token) {
		SeqReadResult result;
		reader.Reposition(0);
		while ((result = await reader.TryReadNext(token)).Success) {
			var rec = result.LogRecord;
			switch (rec.RecordType) {
				case LogRecordType.PartitionType:
					var r = ((PartitionTypeLogRecord) rec).Record;
					if (r.StringPayload != RootPartitionTypeName || r.SubHeader.PartitionId != Guid.Empty)
						throw new InvalidDataException("Unexpected partition type encountered while trying to read the root partition type.");
					RootTypeId = r.Header.RecordId;

					_log.Debug("Root partition type read, id: {id}", RootTypeId);

					break;

				case LogRecordType.Partition:
					var p = ((PartitionLogRecord) rec).Record;
					if (p.StringPayload != RootPartitionName || p.SubHeader.PartitionTypeId != RootTypeId
					                                         || p.SubHeader.ParentPartitionId != Guid.Empty)
						throw new InvalidDataException("Unexpected partition encountered while trying to read the root partition.");
					RootId = p.Header.RecordId;
					recordFactory.SetRootPartitionId(RootId.Value);

					_log.Debug("Root partition read, id: {id}", RootId);

					return;

				case LogRecordType.System:
					var systemLogRecord = (ISystemLogRecord)result.LogRecord;
					if (systemLogRecord.SystemRecordType != SystemRecordType.Epoch)
						throw new ArgumentOutOfRangeException("SystemRecordType", "Unexpected system record while trying to read the root partition");
					continue;

				default:
					throw new ArgumentOutOfRangeException("RecordType", "Unexpected record while trying to read the root partition");
			}
		}
	}
}
