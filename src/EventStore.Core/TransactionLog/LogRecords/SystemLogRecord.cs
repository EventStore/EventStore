// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords;

public enum SystemRecordType : byte {
	Invalid = 0,
	Epoch = 1
}

public enum SystemRecordSerialization : byte {
	Invalid = 0,
	Binary = 1,
	Json = 2,
	Bson = 3
}

public sealed class SystemLogRecord : LogRecord, IEquatable<SystemLogRecord>, ISystemLogRecord {
	public const byte SystemRecordVersion = 0;

	public DateTime TimeStamp { get; private init; }
	public SystemRecordType SystemRecordType { get; private init; }
	public SystemRecordSerialization SystemRecordSerialization { get; private init; }
	public long Reserved { get; private init; }
	public ReadOnlyMemory<byte> Data { get; private init; }

	public SystemLogRecord(long logPosition,
		DateTime timeStamp,
		SystemRecordType systemRecordType,
		SystemRecordSerialization systemRecordSerialization,
		byte[] data)
		: base(LogRecordType.System, SystemRecordVersion, logPosition) {
		TimeStamp = timeStamp;
		SystemRecordType = systemRecordType;
		SystemRecordSerialization = systemRecordSerialization;
		Reserved = 0;
		Data = data ?? NoData;
	}

	private SystemLogRecord(byte version, long logPosition)
		: base(LogRecordType.System, version, logPosition) {

		if (version is not SystemRecordVersion)
			throw new ArgumentException(string.Format(
				"SystemRecord version {0} is incorrect. Supported version: {1}.", version, SystemRecordVersion));
	}

	internal static async ValueTask<SystemLogRecord> ParseAsync(IAsyncBinaryReader reader, byte version, long logPosition, CancellationToken token) {
		return new SystemLogRecord(version, logPosition) {
			TimeStamp = new(await reader.ReadLittleEndianAsync<long>(token)),
			SystemRecordType =
				(SystemRecordType)(await reader.ReadLittleEndianAsync<byte>(token)) is var recordType
				&& recordType is not SystemRecordType.Invalid
					? recordType
					: throw new ArgumentException(
						$"Invalid SystemRecordType {recordType} at LogPosition {logPosition}."),
			SystemRecordSerialization =
				(SystemRecordSerialization)(await reader.ReadLittleEndianAsync<byte>(token)) is var recordSer
				&& recordSer is not SystemRecordSerialization.Invalid
					? recordSer
					: throw new ArgumentException(
						$"Invalid SystemRecordSerialization {recordSer} at LogPosition {logPosition}."),
			Reserved = await reader.ReadLittleEndianAsync<long>(token),
			Data = await reader.ReadLittleEndianAsync<int>(token) is var dataCount && dataCount > 0
				? await reader.ReadBytesAsync(dataCount, token)
				: NoData,
		};
	}

	public EpochRecord GetEpochRecord() {
		if (SystemRecordType != SystemRecordType.Epoch)
			throw new ArgumentException(
				string.Format("Unexpected type of system record. Requested: {0}, actual: {1}.",
					SystemRecordType.Epoch, SystemRecordType));

		switch (SystemRecordSerialization) {
			case SystemRecordSerialization.Json: {
				var dto = Data.ParseJson<EpochRecord.EpochRecordDto>();
				return new EpochRecord(dto);
			}
			default:
				throw new ArgumentOutOfRangeException(
					$"Unexpected SystemRecordSerialization type: {SystemRecordSerialization}",
					"SystemRecordSerialization");
		}
	}

	public override void WriteTo(ref BufferWriterSlim<byte> writer) {
		base.WriteTo(ref writer);

		writer.WriteLittleEndian(TimeStamp.Ticks);
		writer.Add((byte)SystemRecordType);
		writer.Add((byte)SystemRecordSerialization);
		writer.WriteLittleEndian(Reserved);
		writer.Write(Data.Span, LengthFormat.LittleEndian);
	}

	public override int GetSizeWithLengthPrefixAndSuffix() {
		return sizeof(int) * 2	/* Length prefix & suffix */
		       + sizeof(long)	/* TimeStamp */
		       + sizeof(byte)	/* SystemRecordType */
		       + sizeof(byte)	/* SystemRecordSerialization */
		       + sizeof(long)	/* Reserved */
		       + sizeof(int)	/* Data.Length */
		       + Data.Length	/* Data */
		       + BaseSize;
	}

	public bool Equals(SystemLogRecord other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return other.LogPosition == LogPosition
		       && other.TimeStamp.Equals(TimeStamp)
		       && other.SystemRecordType == SystemRecordType
		       && other.SystemRecordSerialization == SystemRecordSerialization
		       && other.Reserved == Reserved;
	}

	public override bool Equals(object obj) {
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != typeof(SystemRecordType)) return false;
		return Equals((SystemLogRecord)obj);
	}

	public override int GetHashCode() {
		unchecked {
			int result = LogPosition.GetHashCode();
			result = (result * 397) ^ TimeStamp.GetHashCode();
			result = (result * 397) ^ SystemRecordType.GetHashCode();
			result = (result * 397) ^ SystemRecordSerialization.GetHashCode();
			result = (result * 397) ^ Reserved.GetHashCode();
			return result;
		}
	}

	public static bool operator ==(SystemLogRecord left, SystemLogRecord right) {
		return Equals(left, right);
	}

	public static bool operator !=(SystemLogRecord left, SystemLogRecord right) {
		return !Equals(left, right);
	}

	public override string ToString() {
		return string.Format("LogPosition: {0}, "
		                     + "TimeStamp: {1}, "
		                     + "SystemRecordType: {2}, "
		                     + "SystemRecordSerialization: {3}, "
		                     + "Reserved: {4}",
			LogPosition,
			TimeStamp,
			SystemRecordType,
			SystemRecordSerialization,
			Reserved);
	}
}
