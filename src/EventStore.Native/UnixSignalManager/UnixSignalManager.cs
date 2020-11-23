using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;

namespace EventStore.Native.UnixSignalManager {
	public class UnixSignalManager {
		private static UnixSignalManager _instance;

		private const int MaxUnixSignals = 64; //max internal limit of UnixSignal instances per process
		private static readonly List<UnixSignal> _handledSignals = new List<UnixSignal>();
		private static readonly object _handledSignalsLock = new object();

		private readonly ConcurrentDictionary<Signum, List<Action>> _actions = new ConcurrentDictionary<Signum, List<Action>>();
		private volatile bool _stop;

		public static UnixSignalManager GetInstance() => _instance ??= new UnixSignalManager();

		private UnixSignalManager() {
			new Thread(HandleSignals) {
				IsBackground = true,
				Name = "UnixSignalManager"
			}.Start();
		}
		public void Subscribe(Signum signum, Action action) {
			lock (_handledSignalsLock) {
				if (_handledSignals.All(x => x.Signum != signum)) {
					if (_handledSignals.Count >= MaxUnixSignals) {
						throw new ArgumentException($"Maximum limit of {MaxUnixSignals} reached for number of handled signals.");
					}

					_handledSignals.Add(new UnixSignal(signum));
				}
			}
			if (_actions.TryGetValue(signum, out List<Action> actions)) {
				actions.Add(action);
			} else {
				_actions[signum] = new List<Action> { action };
			}
		}

		public static void StopProcessing() {
			_instance?.Stop();
		}

		private void Stop() {
			_stop = true;
		}

		private void HandleSignals() {
			const int timeoutMs = 1000;

			while (!_stop) {
				UnixSignal[] handledSignals;
				lock (_handledSignalsLock) {
					handledSignals = _handledSignals.ToArray();
				}

				var index = UnixSignal.WaitAny(handledSignals, TimeSpan.FromMilliseconds(timeoutMs));
				if (index == timeoutMs)
					continue;

				if (_actions.TryGetValue(handledSignals[index].Signum, out List<Action> actions)) {
					foreach (var action in actions) {
						action.Invoke();
					}
				}

				handledSignals[index].Reset();
			}
		}
	}
}
