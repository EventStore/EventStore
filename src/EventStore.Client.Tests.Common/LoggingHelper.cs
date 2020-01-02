using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using EventStore.Client.Logging;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;
using Xunit.Abstractions;

namespace EventStore.Client {
	public static class LoggingHelper {
		private const string CaptureCorrelationId = nameof(CaptureCorrelationId);
		private static readonly Subject<LogEvent> s_logEventSubject = new Subject<LogEvent>();

		private static readonly MessageTemplateTextFormatter s_formatter = new MessageTemplateTextFormatter(
			"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}");

		private static readonly MessageTemplateTextFormatter s_formatterWithException =
			new MessageTemplateTextFormatter(
				"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}");

		static LoggingHelper() {
			if (!Enum.TryParse<LogEventLevel>(Environment.GetEnvironmentVariable("LOGLEVEL"), out var logLevel)) {
				logLevel = LogEventLevel.Fatal;
			}

			Log.Logger = new LoggerConfiguration()
				.WriteTo
				.Observers(observable => observable.Subscribe(s_logEventSubject.OnNext))
				.Enrich.FromLogContext()
				.MinimumLevel.Is(logLevel)
				.CreateLogger();

			ForwardNLogToSerilog();

			LogProvider.Configure = services => services.AddLogging(l => l.AddSerilog());
		}

		private static void ForwardNLogToSerilog() {
			// sic: blindly overwrite the forwarding rules every time
			var target = new SerilogTarget();
			var cfg = new NLog.Config.LoggingConfiguration();
			cfg.AddTarget(nameof(SerilogTarget), target);
			cfg.LoggingRules.Add(new NLog.Config.LoggingRule("*", LogLevel.Trace, target));
			// NB assignment must happen last; rules get ingested upon assignment
			LogManager.Configuration = cfg;
		}

		public static IDisposable Capture(ITestOutputHelper testOutputHelper) {
			var captureId = Guid.NewGuid();

			var callContextData = new AsyncLocal<Tuple<string, Guid>> {
				Value = Tuple.Create(CaptureCorrelationId, captureId)
			};

			bool Filter(LogEvent logEvent) {
				return callContextData.Value.Item2.Equals(captureId);
			}

			var subscription = s_logEventSubject.Subscribe(logEvent => {
				using var writer = new StringWriter();
				if (logEvent.Exception != null) {
					s_formatterWithException.Format(logEvent, writer);
				} else {
					s_formatter.Format(logEvent, writer);
				}

				testOutputHelper.WriteLine(writer.ToString());
			});

			return new DisposableAction(() => {
				subscription.Dispose();
				callContextData.Value = default;
			});
		}

		private class DisposableAction : IDisposable {
			private readonly Action _action;

			public DisposableAction(Action action) {
				_action = action;
			}

			public void Dispose() {
				_action();
			}
		}

		private class Subject<T> : IObserver<T>, IObservable<T> {
			private readonly ConcurrentDictionary<Guid, IObserver<T>> _observers =
				new ConcurrentDictionary<Guid, IObserver<T>>();

			public void OnCompleted() {
				foreach (var observer in _observers.Values) {
					observer.OnCompleted();
				}
			}

			public void OnError(Exception error) {
				foreach (var observer in _observers.Values) {
					observer.OnError(error);
				}
			}

			public void OnNext(T value) {
				foreach (var observer in _observers.Values) {
					observer.OnNext(value);
				}
			}

			public IDisposable Subscribe(IObserver<T> observer) {
				var guid = Guid.NewGuid();
				_observers.TryAdd(guid, observer);

				return new DisposableAction(() => _observers.TryRemove(guid, out _));
			}
		}
	}
}
