using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Management {
	public sealed class ResponseWriter : IResponseWriter {
		private readonly IODispatcher _ioDispatcher;
		private readonly ILogger _logger = LogManager.GetLoggerFor<ResponseWriter>();

		private bool Busy;
		private readonly List<Item> Items = new List<Item>();
		private IODispatcherAsync.CancellationScope _cancellationScope;

		private class Item {
			public string Command;
			public object Body;
		}

		public ResponseWriter(IODispatcher ioDispatcher) {
			_ioDispatcher = ioDispatcher;
			_cancellationScope = new IODispatcherAsync.CancellationScope();
		}

		public void Reset() {
			_logger.Debug("PROJECTIONS: Resetting Master Writer");
			_cancellationScope.Cancel();
			_cancellationScope = new IODispatcherAsync.CancellationScope();
			Items.Clear();
			Busy = false;
		}

		public void PublishCommand(string command, object body) {
			//TODO: PROJECTIONS: Remove before release
			if (!Logging.FilteredMessages.Contains(command)) {
				_logger.Debug(
					"PROJECTIONS: Scheduling the writing of {command} to {stream}. Current status of Writer: Busy: {isBusy}",
					command, ProjectionNamesBuilder._projectionsMasterStream, Busy);
			}

			Items.Add(new Item {Command = command, Body = body});
			if (!Busy) {
				EmitEvents();
			}
		}

		private void EmitEvents() {
			Busy = true;
			var events = Items.Select(CreateEvent).ToArray();
			Items.Clear();
			_ioDispatcher.BeginWriteEvents(
				_cancellationScope,
				ProjectionNamesBuilder._projectionsMasterStream,
				ExpectedVersion.Any,
				SystemAccount.Principal,
				events,
				completed => {
					Busy = false;
					if (completed.Result == OperationResult.Success) {
						foreach (var evt in events) {
							//TODO: PROJECTIONS: Remove before release
							if (!Logging.FilteredMessages.Contains(evt.EventType)) {
								_logger.Debug("PROJECTIONS: Finished writing events to {stream}: {eventType}",
									ProjectionNamesBuilder._projectionsMasterStream, evt.EventType);
							}
						}
					} else {
						_logger.Debug("PROJECTIONS: Failed writing events to {stream} because of {e}: {events}",
							ProjectionNamesBuilder._projectionsMasterStream,
							completed.Result,
							String.Join(",",
								events.Select(x =>
									String.Format("{0}-{1}", x.EventType,
										Helper.UTF8NoBom
											.GetString(x.Data))))); //Can't do anything about it, log and move on
						//throw new Exception(message);
					}

					if (Items.Count > 0)
						EmitEvents();
				}).Run();
		}

		private Event CreateEvent(Item item) {
			return new Event(Guid.NewGuid(), item.Command, true, item.Body.ToJsonBytes(), null);
		}
	}
}
