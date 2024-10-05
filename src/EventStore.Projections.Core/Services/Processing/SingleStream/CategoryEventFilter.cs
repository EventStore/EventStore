// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing.SingleStream;

public class CategoryEventFilter : EventFilter {
	private readonly string _category;
	private readonly string _categoryStream;

	public CategoryEventFilter(string category, bool allEvents, HashSet<string> events)
		: base(allEvents, false, events) {
		_category = category;
		_categoryStream = "$ce-" + category;
	}

	protected override bool DeletedNotificationPasses(string positionStreamId) {
		return _categoryStream == positionStreamId;
	}

	public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId, string eventType) {
		return resolvedFromLinkTo && _categoryStream == positionStreamId;
	}

	public override string GetCategory(string positionStreamId) {
		if (!positionStreamId.StartsWith("$ce-"))
			throw new ArgumentException(string.Format("'{0}' is not a category stream", positionStreamId),
				"positionStreamId");
		return positionStreamId.Substring("$ce-".Length);
	}

	public override string ToString() {
		return string.Format("Category: {0}, CategoryStream: {1}", _category, _categoryStream);
	}
}
