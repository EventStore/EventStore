// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Transactions
{
    [TestFixture]
    public class when_having_two_intermingled_transactions_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _p1;
        private EventRecord _p2;
        private EventRecord _p3;
        private EventRecord _p4;
        private EventRecord _p5;

        private long _t1CommitPos;
        private long _t2CommitPos;

        protected override void WriteTestScenario()
        {
            var t1 = WriteTransactionBegin("ES", ExpectedVersion.NoStream);
            var t2 = WriteTransactionBegin("ABC", ExpectedVersion.NoStream);

            _p1 = WriteTransactionEvent(t1.CorrelationId, t1.LogPosition, 0, t1.EventStreamId, 0, "es1", PrepareFlags.Data);
            _p2 = WriteTransactionEvent(t2.CorrelationId, t2.LogPosition, 0, t2.EventStreamId, 0, "abc1", PrepareFlags.Data);
            _p3 = WriteTransactionEvent(t1.CorrelationId, t1.LogPosition, 1, t1.EventStreamId, 1, "es1", PrepareFlags.Data);
            _p4 = WriteTransactionEvent(t2.CorrelationId, t2.LogPosition, 1, t2.EventStreamId, 1, "abc1", PrepareFlags.Data);
            _p5 = WriteTransactionEvent(t1.CorrelationId, t1.LogPosition, 2, t1.EventStreamId, 2, "es1", PrepareFlags.Data);

            WriteTransactionEnd(t2.CorrelationId, t2.TransactionPosition, t2.EventStreamId);
            WriteTransactionEnd(t1.CorrelationId, t1.TransactionPosition, t1.EventStreamId);

            _t2CommitPos = WriteCommit(t2.CorrelationId, t2.TransactionPosition, t2.EventStreamId, _p2.EventNumber);
            _t1CommitPos = WriteCommit(t1.CorrelationId, t1.TransactionPosition, t1.EventStreamId, _p1.EventNumber);
        }

        [Test]
        public void return_correct_last_event_version_for_larger_stream()
        {
            Assert.AreEqual(2, ReadIndex.GetLastStreamEventNumber("ES"));
        }

        [Test]
        public void return_correct_first_record_for_larger_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 0, out prepare));
            Assert.AreEqual(_p1, prepare);
        }

        [Test]
        public void return_correct_second_record_for_larger_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 1, out prepare));
            Assert.AreEqual(_p3, prepare);
        }

        [Test]
        public void return_correct_third_record_for_larger_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 2, out prepare));
            Assert.AreEqual(_p5, prepare);
        }

        [Test]
        public void not_find_record_with_nonexistent_version_for_larger_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 3, out prepare));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_larger_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p1, records[0]);
            Assert.AreEqual(_p3, records[1]);
            Assert.AreEqual(_p5, records[2]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_larger_stream_with_specific_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", 2, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p5, records[0]);
            Assert.AreEqual(_p3, records[1]);
            Assert.AreEqual(_p1, records[2]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_larger_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", -1, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p5, records[0]);
            Assert.AreEqual(_p3, records[1]);
            Assert.AreEqual(_p1, records[2]);
        }

        [Test]
        public void return_correct_last_event_version_for_smaller_stream()
        {
            Assert.AreEqual(1, ReadIndex.GetLastStreamEventNumber("ABC"));
        }

        [Test]
        public void return_correct_first_record_for_smaller_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ABC", 0, out prepare));
            Assert.AreEqual(_p2, prepare);
        }

        [Test]
        public void return_correct_second_record_for_smaller_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ABC", 1, out prepare));
            Assert.AreEqual(_p4, prepare);
        }

        [Test]
        public void not_find_record_with_nonexistent_version_for_smaller_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ABC", 2, out prepare));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_smaller_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ABC", 0, 2, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_p2, records[0]);
            Assert.AreEqual(_p4, records[1]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_smaller_stream_with_specific_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ABC", 1, 2, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_p4, records[0]);
            Assert.AreEqual(_p2, records[1]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_smaller_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ABC", -1, 2, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_p4, records[0]);
            Assert.AreEqual(_p2, records[1]);
        }

        [Test]
        public void read_all_events_forward_returns_all_events_in_correct_order()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).Records;

            Assert.AreEqual(5, records.Count);
            Assert.AreEqual(_p2, records[0].Event);
            Assert.AreEqual(_p4, records[1].Event);
            Assert.AreEqual(_p1, records[2].Event);
            Assert.AreEqual(_p3, records[3].Event);
            Assert.AreEqual(_p5, records[4].Event);
        }

        [Test]
        public void read_all_events_backward_returns_all_events_in_correct_order()
        {
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            var records = ReadIndex.ReadAllEventsBackward(pos, 10).Records;

            Assert.AreEqual(5, records.Count);
            Assert.AreEqual(_p5, records[0].Event);
            Assert.AreEqual(_p3, records[1].Event);
            Assert.AreEqual(_p1, records[2].Event);
            Assert.AreEqual(_p4, records[3].Event);
            Assert.AreEqual(_p2, records[4].Event);
        }

        [Test]
        public void read_all_events_forward_returns_nothing_when_prepare_position_is_greater_than_last_prepare_in_commit()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(_t1CommitPos, _t1CommitPos), 10).Records;
            Assert.AreEqual(0, records.Count);
        }

        [Test]
        public void read_all_events_backwards_returns_nothing_when_prepare_position_is_smaller_than_first_prepare_in_commit()
        {
            var records = ReadIndex.ReadAllEventsBackward(new TFPos(_t2CommitPos, 0), 10).Records;
            Assert.AreEqual(0, records.Count);
        }

        [Test]
        public void read_all_events_forward_returns_correct_events_starting_in_the_middle_of_tf()
        {
            var res1 = ReadIndex.ReadAllEventsForward(new TFPos(_t2CommitPos, _p4.LogPosition), 10);

            Assert.AreEqual(4, res1.Records.Count);
            Assert.AreEqual(_p4, res1.Records[0].Event);
            Assert.AreEqual(_p1, res1.Records[1].Event);
            Assert.AreEqual(_p3, res1.Records[2].Event);
            Assert.AreEqual(_p5, res1.Records[3].Event);

            var res2 = ReadIndex.ReadAllEventsBackward(res1.PrevPos, 10);
            Assert.AreEqual(1, res2.Records.Count);
            Assert.AreEqual(_p2, res2.Records[0].Event);
        }

        [Test]
        public void read_all_events_backward_returns_correct_events_starting_in_the_middle_of_tf()
        {
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), _p4.LogPosition); // p3 post-pos
            var res1 = ReadIndex.ReadAllEventsBackward(pos, 10);

            Assert.AreEqual(4,   res1.Records.Count);
            Assert.AreEqual(_p3, res1.Records[0].Event);
            Assert.AreEqual(_p1, res1.Records[1].Event);
            Assert.AreEqual(_p4, res1.Records[2].Event);
            Assert.AreEqual(_p2, res1.Records[3].Event);

            var res2 = ReadIndex.ReadAllEventsForward(res1.PrevPos, 10);
            Assert.AreEqual(1, res2.Records.Count);
            Assert.AreEqual(_p5, res2.Records[0].Event);
        }

        [Test]
        public void all_records_can_be_read_sequentially_page_by_page_in_forward_pass()
        {
            var recs = new[] {_p2, _p4, _p1, _p3, _p5}; // in committed order

            int count = 0;
            var pos = new TFPos(0, 0);
            IndexReadAllResult result;
            while ((result = ReadIndex.ReadAllEventsForward(pos, 1)).Records.Count != 0)
            {
                Assert.AreEqual(1, result.Records.Count);
                Assert.AreEqual(recs[count], result.Records[0].Event);
                pos = result.NextPos;
                count += 1;
            }
            Assert.AreEqual(recs.Length, count);
        }

        [Test]
        public void all_records_can_be_read_sequentially_page_by_page_in_backward_pass()
        {
            var recs = new[] { _p5, _p3, _p1, _p4, _p2 }; // in reverse committed order

            int count = 0;
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            IndexReadAllResult result;
            while ((result = ReadIndex.ReadAllEventsBackward(pos, 1)).Records.Count != 0)
            {
                Assert.AreEqual(1, result.Records.Count);
                Assert.AreEqual(recs[count], result.Records[0].Event);
                pos = result.NextPos;
                count += 1;
            }
            Assert.AreEqual(recs.Length, count);
        }

        [Test]
        public void position_returned_for_prev_page_when_traversing_forward_allow_to_traverse_backward_correctly()
        {
            var recs = new[] { _p2, _p4, _p1, _p3, _p5 }; // in committed order

            int count = 0;
            var pos = new TFPos(0, 0);
            IndexReadAllResult result;
            while ((result = ReadIndex.ReadAllEventsForward(pos, 1)).Records.Count != 0)
            {
                Assert.AreEqual(1, result.Records.Count);
                Assert.AreEqual(recs[count], result.Records[0].Event);

                var localPos = result.PrevPos;
                int localCount = 0;
                IndexReadAllResult localResult;
                while ((localResult = ReadIndex.ReadAllEventsBackward(localPos, 1)).Records.Count != 0)
                {
                    Assert.AreEqual(1, localResult.Records.Count);
                    Assert.AreEqual(recs[count - 1 - localCount], localResult.Records[0].Event);
                    localPos = localResult.NextPos;
                    localCount += 1;
                }

                pos = result.NextPos;
                count += 1;
            }
            Assert.AreEqual(recs.Length, count);
        }

        [Test]
        public void position_returned_for_prev_page_when_traversing_backward_allow_to_traverse_forward_correctly()
        {
            var recs = new[] { _p5, _p3, _p1, _p4, _p2 }; // in reverse committed order

            int count = 0;
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            IndexReadAllResult result;
            while ((result = ReadIndex.ReadAllEventsBackward(pos, 1)).Records.Count != 0)
            {
                Assert.AreEqual(1, result.Records.Count);
                Assert.AreEqual(recs[count], result.Records[0].Event);

                var localPos = result.PrevPos;
                int localCount = 0;
                IndexReadAllResult localResult;
                while ((localResult = ReadIndex.ReadAllEventsForward(localPos, 1)).Records.Count != 0)
                {
                    Assert.AreEqual(1, localResult.Records.Count);
                    Assert.AreEqual(recs[count - 1 - localCount], localResult.Records[0].Event);
                    localPos = localResult.NextPos;
                    localCount += 1;
                }

                pos = result.NextPos;
                count += 1;
            }
            Assert.AreEqual(recs.Length, count);
        }
    }
}