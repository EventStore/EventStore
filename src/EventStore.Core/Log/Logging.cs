// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Reactive.Subjects;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace EventStore.Common.Log;

public class FrontEndLogging : ILogEventSink, IObservable<LogEvent> {
	public static readonly FrontEndLogging Instance = new();

	static readonly ILogger Log = Serilog.Log.ForContext<FrontEndLogging>();

	readonly Subject<LogEvent> _subject = new();

	public void Emit(LogEvent logEvent) {
		_subject.OnNext(logEvent);
	}

	public IDisposable Subscribe(IObserver<LogEvent> observer) {
		return _subject.Subscribe(observer);
	}
}
