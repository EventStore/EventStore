// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using DotNext.Buffers.Binary;
using DotNext.IO;
using DotNext.Text;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords;

[Flags]
public enum PrepareFlags : ushort {
	None = 0x00,
	Data = 0x01, // prepare contains data
	TransactionBegin = 0x02, // prepare starts transaction
	TransactionEnd = 0x04, // prepare ends transaction
	StreamDelete = 0x08, // prepare deletes stream

	IsCommitted = 0x20, // prepare should be considered committed immediately, no commit will follow in TF
	//Update = 0x30,                  // prepare updates previous instance of the same event, DANGEROUS!

	IsJson = 0x100, // indicates data & metadata are valid json
	IsRedacted = 0x200,

	// aggregate flag set
	// unused and easily confused with StreamDelete:  DeleteTombstone = TransactionBegin | TransactionEnd | StreamDelete,
	SingleWrite = Data | TransactionBegin | TransactionEnd
}

public static class PrepareFlagsExtensions {
	public static bool HasAllOf(this PrepareFlags flags, PrepareFlags flagSet) {
		return (flags & flagSet) == flagSet;
	}

	public static bool HasAnyOf(this PrepareFlags flags, PrepareFlags flagSet) {
		return (flags & flagSet) != 0;
	}

	public static bool HasNoneOf(this PrepareFlags flags, PrepareFlags flagSet) {
		return (flags & flagSet) == 0;
	}
}

public class PrepareLogRecord : LogRecord, IEquatable<PrepareLogRecord>, IPrepareLogRecord<string> {
	public const byte PrepareRecordVersion = 1;

	public PrepareFlags Flags { get; private init; }
	public long TransactionPosition { get; private init; }
	public int TransactionOffset { get; private init; }
	public long ExpectedVersion { get; private init; } // if IsCommitted is set, this is final EventNumber
	public string EventStreamId { get; private init; }
	private int? _eventStreamIdSize;
	public Guid EventId { get; private init; }
	public Guid CorrelationId { get; private init; }
	public DateTime TimeStamp { get; private init; }
	public string EventType { get; private init; }
	private int? _eventTypeSize;
	public ReadOnlyMemory<byte> Data => Flags.HasFlag(PrepareFlags.IsRedacted) ? NoData : _dataOnDisk;
	private ReadOnlyMemory<byte> _dataOnDisk;
	public ReadOnlyMemory<byte> Metadata { get; private init; }

	private int? _sizeOnDisk;

	public long InMemorySize {
		get {
			return sizeof(LogRecordType)
			       + 1
			       + 8
			       + sizeof(PrepareFlags)
			       + 8
			       + 4
			       + 4
			       + IntPtr.Size + EventStreamId.Length * 2
			       + 16
			       + 16
			       + 8
			       + IntPtr.Size + EventType.Length * 2
			       + IntPtr.Size + _dataOnDisk.Length
			       + IntPtr.Size + Metadata.Length;
		}
	}

	// including length and suffix
	public int SizeOnDisk {
		get {
			if (_sizeOnDisk.HasValue)
				return _sizeOnDisk.Value;

			_eventStreamIdSize ??= Encoding.UTF8.GetByteCount(EventStreamId);
			_eventTypeSize ??= Encoding.UTF8.GetByteCount(EventType);

			_sizeOnDisk =
				2 * sizeof(int) /* Length prefix & suffix */
				+ sizeof(byte) /* Record Type */
				+ sizeof(byte) /* Version */
				+ sizeof(long) /* Log Position */
				+ sizeof(ushort) /* Flags */
				+ sizeof(long) /* TransactionPosition */
				+ sizeof(int) /* TransactionOffset */
				+ (Version == LogRecordVersion.LogRecordV0 ? sizeof(int) : sizeof(long)) /* ExpectedVersion */
				+ StringSizeWithLengthPrefix(_eventStreamIdSize.Value) /* EventStreamId */
				+ 16 /* EventId */
				+ 16 /* CorrelationId */
				+ sizeof(long) /* TimeStamp */
				+ StringSizeWithLengthPrefix(_eventTypeSize.Value) /* EventType */
				+ sizeof(int) /* Data length */
				+ _dataOnDisk.Length /* Data */
				+ sizeof(int) /* Metadata length */
				+ Metadata.Length; /* Metadata */

			return _sizeOnDisk.Value;
		}
	}

	private static int StringSizeWithLengthPrefix(int stringSize) {
		// when written to disk by the BinaryWriter, a string is prefixed with its length which is written as a 7-bit encoded integer
		Ensure.Nonnegative(stringSize, nameof(stringSize));

		if (stringSize == 0)
			return 1;

		var msb = 31 - BitOperations.LeadingZeroCount((uint)stringSize);
		return stringSize + msb / 7 + 1;
	}

	public PrepareLogRecord(long logPosition,
		Guid correlationId,
		Guid eventId,
		long transactionPosition,
		int transactionOffset,
		string eventStreamId,
		int? eventStreamIdSize,
		long expectedVersion,
		DateTime timeStamp,
		PrepareFlags flags,
		string eventType,
		int? eventTypeSize,
		ReadOnlyMemory<byte> data,
		ReadOnlyMemory<byte> metadata,
		byte prepareRecordVersion = PrepareRecordVersion)
		: base(LogRecordType.Prepare, prepareRecordVersion, logPosition) {
		Ensure.NotEmptyGuid(correlationId, "correlationId");
		Ensure.NotEmptyGuid(eventId, "eventId");
		Ensure.Nonnegative(transactionPosition, "transactionPosition");
		if (transactionOffset < -1)
			throw new ArgumentOutOfRangeException("transactionOffset");
		Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
		if (expectedVersion < Core.Data.ExpectedVersion.Any)
			throw new ArgumentOutOfRangeException("expectedVersion");

		Flags = flags;
		TransactionPosition = transactionPosition;
		TransactionOffset = transactionOffset;
		ExpectedVersion = expectedVersion;
		EventStreamId = eventStreamId;
		_eventStreamIdSize = eventStreamIdSize;

		EventId = eventId;
		CorrelationId = correlationId;
		TimeStamp = timeStamp;
		EventType = eventType ?? string.Empty;
		_eventTypeSize = eventTypeSize;
		_dataOnDisk = data;
		Metadata = metadata;
		if (InMemorySize > TFConsts.MaxLogRecordSize) throw new Exception("Record too large.");
	}

	private PrepareLogRecord(byte version, long logPosition)
		: base(LogRecordType.Prepare, version, logPosition) {

		if (version is not LogRecordVersion.LogRecordV0 and not LogRecordVersion.LogRecordV1)
			throw new ArgumentException(string.Format(
				"PrepareRecord version {0} is incorrect. Supported version: {1}.", version, PrepareRecordVersion));
	}

	internal static async ValueTask<PrepareLogRecord> ParseAsync(IAsyncBinaryReader reader, byte version,
		long logPosition, CancellationToken token) {
		var context = new DecodingContext(Encoding.UTF8, reuseDecoder: true);

		var record = new PrepareLogRecord(version, logPosition) {
			Flags = (PrepareFlags)await reader.ReadLittleEndianAsync<ushort>(token),
			TransactionPosition = await reader.ReadLittleEndianAsync<long>(token),
			TransactionOffset = await reader.ReadLittleEndianAsync<int>(token),
			ExpectedVersion = version is LogRecordVersion.LogRecordV0
				? AdjustVersion(await reader.ReadLittleEndianAsync<int>(token))
				: await reader.ReadLittleEndianAsync<long>(token),
			EventStreamId = await reader.ReadStringAsync(context, token),
			EventId = (await reader.ReadAsync<Blittable<Guid>>(token)).Value,
			CorrelationId = (await reader.ReadAsync<Blittable<Guid>>(token)).Value,
			TimeStamp = new(await reader.ReadLittleEndianAsync<long>(token)),
			EventType = await reader.ReadStringAsync(context, token),
			_dataOnDisk = await reader.ReadLittleEndianAsync<int>(token) is var dataCount && dataCount > 0
				? await reader.ReadBytesAsync(dataCount, token)
				: NoData,
			Metadata = await reader.ReadLittleEndianAsync<int>(token) is var metadataCount && metadataCount > 0
				? await reader.ReadBytesAsync(metadataCount, token)
				: NoData,
		};

		if (record.InMemorySize > TFConsts.MaxLogRecordSize) throw new Exception("Record too large.");

		return record;

		static long AdjustVersion(int version)
			=> version is int.MaxValue - 1 ? long.MaxValue - 1L : version;
	}

	public IPrepareLogRecord<string> CopyForRetry(long logPosition, long transactionPosition) {
		return new PrepareLogRecord(
			logPosition: logPosition,
			correlationId: CorrelationId,
			eventId: EventId,
			transactionPosition: transactionPosition,
			transactionOffset: TransactionOffset,
			eventStreamId: EventStreamId,
			eventStreamIdSize: _eventStreamIdSize,
			expectedVersion: ExpectedVersion,
			timeStamp: TimeStamp,
			flags: Flags,
			eventType: EventType,
			eventTypeSize: _eventTypeSize,
			data: _dataOnDisk,
			metadata: Metadata);
	}

	public override void WriteTo(ref BufferWriterSlim<byte> writer) {
		base.WriteTo(ref writer);

		writer.WriteLittleEndian((ushort)Flags);
		writer.WriteLittleEndian(TransactionPosition);
		writer.WriteLittleEndian(TransactionOffset);
		if (Version is LogRecordVersion.LogRecordV0) {
			int expectedVersion = ExpectedVersion == long.MaxValue - 1 ? int.MaxValue - 1 : (int)ExpectedVersion;
			writer.WriteLittleEndian(expectedVersion);
		} else {
			writer.WriteLittleEndian(ExpectedVersion);
		}

		_eventStreamIdSize ??= Encoding.UTF8.GetByteCount(EventStreamId);
		writer.WriteLeb128(_eventStreamIdSize.GetValueOrDefault());
		var buffer = writer.GetSpan(_eventStreamIdSize.GetValueOrDefault());
		writer.Advance(Encoding.UTF8.GetBytes(EventStreamId, buffer));

		buffer = writer.GetSpan(16);
		EventId.TryWriteBytes(buffer);
		writer.Advance(16);

		buffer = writer.GetSpan(16);
		CorrelationId.TryWriteBytes(buffer);
		writer.Advance(16);

		writer.WriteLittleEndian(TimeStamp.Ticks);

		_eventTypeSize ??= Encoding.UTF8.GetByteCount(EventType);
		writer.WriteLeb128(_eventTypeSize.GetValueOrDefault());
		buffer = writer.GetSpan(_eventTypeSize.GetValueOrDefault());
		writer.Advance(Encoding.UTF8.GetBytes(EventType, buffer));

		writer.WriteLittleEndian(_dataOnDisk.Length);
		writer.Write(_dataOnDisk.Span);
		writer.WriteLittleEndian(Metadata.Length);
		writer.Write(Metadata.Span);
	}

	public bool Equals(PrepareLogRecord other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return other.LogPosition == LogPosition
		       && other.Flags == Flags
		       && other.TransactionPosition == TransactionPosition
		       && other.TransactionOffset == TransactionOffset
		       && other.ExpectedVersion == ExpectedVersion
		       && other.EventStreamId.Equals(EventStreamId)
		       && other.EventId == EventId
		       && other.CorrelationId == CorrelationId
		       && other.TimeStamp.Equals(TimeStamp)
		       && other.EventType.Equals(EventType)
		       && other.Data.Span.SequenceEqual(Data.Span)
		       && other._dataOnDisk.Span.SequenceEqual(_dataOnDisk.Span)
		       && other.Metadata.Span.SequenceEqual(Metadata.Span);
	}

	public override bool Equals(object obj) {
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != typeof(PrepareLogRecord)) return false;
		return Equals((PrepareLogRecord)obj);
	}

	public override int GetHashCode() {
		unchecked {
			int result = LogPosition.GetHashCode();
			result = (result * 397) ^ Flags.GetHashCode();
			result = (result * 397) ^ TransactionPosition.GetHashCode();
			result = (result * 397) ^ TransactionOffset;
			result = (result * 397) ^ ExpectedVersion.GetHashCode();
			result = (result * 397) ^ EventStreamId.GetHashCode();

			result = (result * 397) ^ EventId.GetHashCode();
			result = (result * 397) ^ CorrelationId.GetHashCode();
			result = (result * 397) ^ TimeStamp.GetHashCode();
			result = (result * 397) ^ EventType.GetHashCode();
			result = (result * 397) ^ _dataOnDisk.GetHashCode();
			result = (result * 397) ^ Metadata.GetHashCode();
			return result;
		}
	}

	public static bool operator ==(PrepareLogRecord left, PrepareLogRecord right) {
		return Equals(left, right);
	}

	public static bool operator !=(PrepareLogRecord left, PrepareLogRecord right) {
		return !Equals(left, right);
	}

	public override string ToString() {
		return string.Format("LogPosition: {0}, "
		                     + "Flags: {1}, "
		                     + "TransactionPosition: {2}, "
		                     + "TransactionOffset: {3}, "
		                     + "ExpectedVersion: {4}, "
		                     + "EventStreamId: {5}, "
		                     + "EventId: {6}, "
		                     + "CorrelationId: {7}, "
		                     + "TimeStamp: {8}, "
		                     + "EventType: {9}, "
		                     + "InMemorySize: {10}",
			LogPosition,
			Flags,
			TransactionPosition,
			TransactionOffset,
			ExpectedVersion,
			EventStreamId,
			EventId,
			CorrelationId,
			TimeStamp,
			EventType,
			InMemorySize);
	}
}
