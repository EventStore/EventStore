// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Duck;

class IndexRecord {
	public int event_number { get; set; }
	public long log_position { get; set; }
}

class CategoryRecord {
	public int category_seq { get; set; }
	public long log_position { get; set; }
	public int event_number { get; set; }
	public long event_type { get; set; }
	public long stream { get; set; }
}

class EventTypeRecord {
	public int event_type_seq { get; set; }
	public long log_position { get; set; }
	public int event_number { get; set; }
	public long stream { get; set; }
}

class AllRecord {
	public long seq { get; set; }
	public long log_position { get; set; }
	public int event_number { get; set; }
	public int event_type { get; set; }
	public long stream { get; set; }
}

public class ReferenceRecord {
	public long id { get; set; }
	public string name { get; set; }
}
