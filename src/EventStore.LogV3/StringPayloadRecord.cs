// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text;

namespace EventStore.LogV3;

// Several records have a payload which is a dynamically sized string
public struct StringPayloadRecord {
	public static StringPayloadRecord<TSubHeader> Create<TSubHeader>(RecordView<TSubHeader> record)
		where TSubHeader : unmanaged =>
		new(record);

	public static StringPayloadRecord<TSubHeader> Create<TSubHeader>(MutableRecordView<TSubHeader> record)
		where TSubHeader : unmanaged =>
		new(record);
}

public struct StringPayloadRecord<TSubHeader> : IRecordView where TSubHeader : unmanaged {
	public ReadOnlyMemory<byte> Bytes => Record.Bytes;
	public RecordView<TSubHeader> Record { get; }

	public ref readonly Raw.RecordHeader Header => ref Record.Header;
	public ref readonly TSubHeader SubHeader => ref Record.SubHeader;
	public ReadOnlyMemory<byte> Payload => Record.Payload;
	public string StringPayload => Encoding.UTF8.GetString(Record.Payload.Span);

	public StringPayloadRecord(RecordView<TSubHeader> record) {
		Record = record;
	}
}
