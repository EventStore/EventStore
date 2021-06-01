using System;
using System.Collections.Concurrent;
using EventStore.Core.LogAbstraction;
using Serilog;
using Value = System.UInt32;

namespace EventStore.Core.LogV3 {
	// There are two components to the NameIndex. The NameIndex class and the INameIndexPersistence
	// implementation which is injected.
	//
	// The NameIndex allows for reservation of entries, which generates the numbering and holds them
	// in memory. This is similar to the purpose of the 'IndexWriter' class.
	//
	// The entries can then be confirmed, which transfers them to the INameIndexPersistence
	// object which is allowed to persist them to disk. This is similar to the IndexCommiter class.
	//
	// Components wanting only entries that have been confirmed will read from the INameIndexPersistence.
	public class NameIndex :
		INameIndex<Value>,
		INameIndexConfirmer<Value> {

		private static readonly ILogger Log = Serilog.Log.ForContext<NameIndex>();
		private readonly ConcurrentDictionary<string, Value> _reservations = new();
		private readonly INameIndexPersistence<Value> _persistence;
		private readonly string _indexName;
		private readonly Value _firstValue;
		private readonly Value _valueInterval;
		private readonly object _nextValueLock = new();
		private Value _nextValue;

		public NameIndex(
			string indexName,
			Value firstValue,
			Value valueInterval,
			INameIndexPersistence<Value> persistence) {

			_indexName = indexName;
			_firstValue = firstValue;
			_valueInterval = valueInterval;
			_nextValue = firstValue;
			_persistence = persistence;
		}

		public void Dispose() {
			_persistence?.Dispose();
		}

		public void CancelReservations() {
			var count = _reservations.Count;
			_reservations.Clear();
			var nextValue = CalcNextValue();
			Log.Information("{indexName} {count} reservations cancelled. Next value is {value}",
				_indexName, count, nextValue);
		}

		public void InitializeWithConfirmed(INameLookup<Value> source) {
			_reservations.Clear();
			_persistence.Init(source);
			var nextValue = CalcNextValue();
			Log.Information("{indexName} initialized. Next value is {value}", _indexName, nextValue);
		}

		Value CalcNextValue() {
			lock (_nextValueLock) {
				_nextValue = _persistence.LastValueAdded == default
					? _firstValue
					: _persistence.LastValueAdded + _valueInterval;
				return _nextValue;
			}
		}

		public void Confirm(string name, Value value) {
			_persistence.Add(name, value);
			if (_reservations.TryRemove(name, out var reservedValue)) {
				if (reservedValue != value) {
					throw new Exception($"This should never happen. Confirmed value for \"{name}\" was {value} but reserved as {reservedValue}");
				}
			} else {
				// an entry got confirmed that we didn't reserve. this is normal in the follower
				// and there is nothing to do. however it is currently possible in the leader too
				// because it only waits for the chaser to catch up and not the index.
				// in this case we need to maintain _nextValue
				lock (_nextValueLock) {
					_nextValue = value + _valueInterval;
				}
			}
		}

		public bool GetOrReserve(string name, out Value value, out Value addedValue, out string addedName) {
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));

			if (_reservations.TryGetValue(name, out value) ||
				_persistence.TryGetValue(name, out value)) {

				addedValue = default;
				addedName = default;
				return true;
			}

			Reserve(name, out value, out addedValue, out addedName);
			return false;
		}

		public void Reserve(string name, out Value value, out Value addedValue, out string addedName) {
			lock (_nextValueLock) {
				value = _nextValue;
				_nextValue += _valueInterval;
				addedValue = value;
				addedName = name;
				_reservations[name] = value;
				Log.Debug("{indexName} reserved new entry: {key}:{value}", _indexName, name, value);
			}
		}
	}
}
