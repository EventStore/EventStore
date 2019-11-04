using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using System.Linq;

namespace EventStore.Core.Tests.Helpers {
	public class ManualQueue : IPublisher, IHandle<TimerMessage.Schedule> {
		private readonly Queue<Message> _queue = new Queue<Message>();
		private readonly IBus _bus;
		private readonly ITimeProvider _time;
		private readonly List<InternalSchedule> _timerQueue = new List<InternalSchedule>();
		private bool _timerDisabled;

		public ManualQueue(IBus bus, ITimeProvider time) {
			_bus = bus;
			_time = time;
			_bus.Subscribe(this);
		}

		public void Publish(Message message) {
			_queue.Enqueue(message);
		}

		public int Process() {
			ProcessTimer();
			return ProcessNonTimer();
		}

		public int ProcessNonTimer() {
			var count = 0;
			while (_queue.Count > 0) {
				var message = _queue.Dequeue();
				_bus.Publish(message);
				count++;
				if (count > 1000)
					throw new Exception("Possible infinite message loop");
			}

			return count;
		}

		public void ProcessTimer() {
			if (!_timerDisabled) {
				var orderedTimerMessages = _timerQueue.OrderBy(v => v.Scheduled.Add(v.Message.TriggerAfter)).ToArray();
				_timerQueue.Clear();
				foreach (var timerMessage in orderedTimerMessages) {
					if (timerMessage.Scheduled.Add(timerMessage.Message.TriggerAfter) <= _time.UtcNow)
						timerMessage.Message.Reply();
					else
						_timerQueue.Add(timerMessage);
				}
			}
		}

		public void DisableTimer() {
			_timerDisabled = true;
		}

		public void EnableTimer(bool process = true) {
			_timerDisabled = false;
			if (process)
				Process();
		}

		private class InternalSchedule {
			public readonly TimerMessage.Schedule Message;
			public readonly DateTime Scheduled;

			public InternalSchedule(TimerMessage.Schedule message, DateTime scheduled) {
				Message = message;
				Scheduled = scheduled;
			}
		}

		public void Handle(TimerMessage.Schedule message) {
			_timerQueue.Add(new InternalSchedule(message, _time.UtcNow));
		}
	}
}
