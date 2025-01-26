// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using EventStore.Common.Log;
using Serilog.Events;

namespace EventStore.ClusterNode.Services;

class LogObserver : IObserver<LogEvent>, IDisposable {
	const int MaxCount = 200;
	public readonly ObservableCollection<LogEvent> Items = [];
	readonly IDisposable _sub;

	public LogObserver() {
		_sub = FrontEndLogging.Instance.Subscribe(this);
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnNext(LogEvent value) {
		Items.Add(value);
		if (Items.Count > MaxCount) {
			Items.RemoveAt(0);
		}
		PropertyChanged?.Invoke(this, new(nameof(Items)));
	}

	public void OnCompleted() {
	}

	public void OnError(Exception error) {
	}

	public void Dispose() {
		_sub?.Dispose();
	}
}
