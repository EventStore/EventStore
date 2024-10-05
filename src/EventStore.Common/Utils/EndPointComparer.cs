// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Net;

namespace EventStore.Common.Utils;

public class EndPointComparer : IComparer<EndPoint> {
	public int Compare(EndPoint x, EndPoint y) {
		if (ReferenceEquals(x, y)) return 0;
		if (ReferenceEquals(null, y)) return 1;
		if (ReferenceEquals(null, x)) return -1;
		var portCompare = x.GetPort().CompareTo(y.GetPort());
		if (portCompare != 0) return portCompare;
		return string.Compare(x.GetHost(), y.GetHost(), StringComparison.Ordinal);
	}
}

public class EndPointEqualityComparer : IEqualityComparer<EndPoint> {
	public bool Equals(EndPoint x, EndPoint y) {
		return x.GetHost().Equals(y.GetHost()) && x.GetPort() == y.GetPort();
	}

	public int GetHashCode(EndPoint obj) {
            return obj.GetHost().GetHashCode() ^ obj.GetPort();
	}
}
