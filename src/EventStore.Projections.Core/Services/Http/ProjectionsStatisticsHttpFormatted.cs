// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Http;

public class ProjectionsStatisticsHttpFormatted {
	private readonly ProjectionStatisticsHttpFormatted[] _projections;

	public ProjectionsStatisticsHttpFormatted(
		ProjectionManagementMessage.Statistics source, Func<string, string> makeAbsouteUrl) {
		_projections =
			source.Projections.Select(v => new ProjectionStatisticsHttpFormatted(v, makeAbsouteUrl)).ToArray();
	}

	public ProjectionStatisticsHttpFormatted[] Projections {
		get { return _projections; }
	}
}
