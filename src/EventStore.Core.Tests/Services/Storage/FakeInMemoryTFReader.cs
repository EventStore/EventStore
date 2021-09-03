using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.Storage {
	public class FakeInMemoryTfReader<TStreamId> : ITransactionFileReader {
		private Dictionary<long, ILogRecord> _records = new Dictionary<long, ILogRecord>();
		private long _curPosition = 0;
		private int _recordOffset;

		public FakeInMemoryTfReader(int recordOffset){
			_recordOffset = recordOffset;
		}

		public void AddRecord(ILogRecord record, long position){
			_records.Add(position, record);
			if (record is IPrepareLogRecord<TStreamId> prepare) {
				foreach(var eventRecord in prepare.Events) {
					if (eventRecord.EventLogPosition!.Value == position) continue;
					_records.Add(eventRecord.EventLogPosition!.Value, record);
				}
			}
		}

		public void Reposition(long position) {
			_curPosition = position;
		}

		public SeqReadResult TryReadNext() {
			if(_records.ContainsKey(_curPosition)){
				var pos = _curPosition;
				_curPosition += _recordOffset;
				return new SeqReadResult(true, false, _records[pos], _recordOffset, pos, pos + _recordOffset);
			} else{
				return new SeqReadResult(false, false, null, 0, 0, 0);
			}
		}

		public SeqReadResult TryReadPrev() {
			throw new NotImplementedException();
		}

		public RecordReadResult TryReadAt(long position) {
			if(_records.ContainsKey(position)){
				return new RecordReadResult(true, 0, _records[position], 0);
			} else{
				return new RecordReadResult(false, 0, _records[position], 0);
			}
		}

		public bool ExistsAt(long position) {
			return _records.ContainsKey(position);
		}
	}
}
