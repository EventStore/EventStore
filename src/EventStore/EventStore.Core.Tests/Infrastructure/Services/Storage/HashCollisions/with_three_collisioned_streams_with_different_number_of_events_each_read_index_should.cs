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
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage.HashCollisions
{
    [TestFixture]
    public class with_three_collisioned_streams_with_different_number_of_events_each_read_index_should : ReadIndexTestScenario
    {
        private EventRecord[] _prepares1;
        private EventRecord[] _prepares2;
        private EventRecord[] _prepares3;

        protected override void WriteTestScenario()
        {
            _prepares1 = new EventRecord[3];
            for (int i = 0; i < _prepares1.Length; i++)
            {
                _prepares1[i] = WriteSingleEvent("AB", i, "test" + i);
            }

            _prepares2 = new EventRecord[5];
            for (int i = 0; i < _prepares2.Length; i++)
            {
                _prepares2[i] = WriteSingleEvent("CD", i, "test" + i);
            }

            _prepares3 = new EventRecord[7];
            for (int i = 0; i < _prepares3.Length; i++)
            {
                _prepares3[i] = WriteSingleEvent("EF", i, "test" + i);
            }
        }

        #region first

        [Test]
        public void return_correct_last_event_version_for_first_stream()
        {
            Assert.AreEqual(2, ReadIndex.GetLastStreamEventNumber("AB"));
        }

        [Test]
        public void return_minus_one_when_asked_for_last_version_for_stream_with_same_hash_as_first()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("FY"));
        }

        [Test]
        public void return_correct_first_record_for_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("AB", 0, out record));
            Assert.AreEqual(_prepares1[0], record);
        }

        [Test]
        public void return_correct_last_log_record_for_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("AB", 2, out record));
            Assert.AreEqual(_prepares1[2], record);
        }

        [Test]
        public void not_find_record_with_version_3_in_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("AB", 3, out record));
        }

        [Test]
        public void return_not_found_for_record_version_3_for_stream_with_same_hash_as_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 3, out record));
        }

        [Test]
        public void return_not_found_for_record_version_2_for_stream_with_same_hash_as_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 2, out record));
        }

        [Test]
        public void return_not_found_for_record_version_0_for_stream_with_same_hash_as_first_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 0, out record));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("AB", 0, 3, out records));
            Assert.AreEqual(3, records.Length);

            for (int i = 0; i < _prepares1.Length; i++)
            {
                Assert.AreEqual(_prepares1[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_1_range_on_from_start_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("AB", 0, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares1[0], records[0]);
        }

        [Test]
        public void return_correct_1_1_range_on_from_start_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("AB", 1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares1[1], records[0]);
        }

        [Test]
        public void return_empty_range_for_3_1_range_on_from_start_range_query_request_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("AB", 3, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 0, 3, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_1_1_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 1, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_3_1_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 3, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_first_stream_with_specific_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", 2, 3, out records));
            Assert.AreEqual(3, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares1.Length; i++)
            {
                Assert.AreEqual(_prepares1[i], records[i]);
            }
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_first_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", -1, 3, out records));
            Assert.AreEqual(3, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares1.Length; i++)
            {
                Assert.AreEqual(_prepares1[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_1_range_on_from_end_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", 0, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares1[0], records[0]);
        }

        [Test]
        public void return_correct_from_end_1_range_on_from_end_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", -1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares1[2], records[0]);
        }

        [Test]
        public void return_correct_1_1_range_on_from_end_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", 1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares1[1], records[0]);
        }

        [Test]
        public void return_empty_range_for_3_1_range_on_from_end_range_query_request_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("AB", 3, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 0, 3, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_1_1_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 1, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_3_1_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_first_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 3, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        #endregion

        #region second

        [Test]
        public void return_correct_last_event_version_for_second_stream()
        {
            Assert.AreEqual(4, ReadIndex.GetLastStreamEventNumber("CD"));
        }

        [Test]
        public void return_minus_one_when_aked_for_last_version_for_stream_with_same_hash_as_second()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("FY"));
        }

        [Test]
        public void return_correct_first_record_for_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("CD", 0, out record));
            Assert.AreEqual(_prepares2[0], record);
        }

        [Test]
        public void return_correct_last_log_record_for_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("CD", 4, out record));
            Assert.AreEqual(_prepares2[4], record);
        }

        [Test]
        public void not_find_record_with_version_5_in_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("CD", 5, out record));
        }

        [Test]
        public void return_not_found_for_record_version_5_for_stream_with_same_hash_as_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 5, out record));
        }

        [Test]
        public void return_not_found_for_record_version_4_for_stream_with_same_hash_as_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 4, out record));
        }

        [Test]
        public void return_not_found_for_record_version_0_for_stream_with_same_hash_as_second_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 0, out record));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("CD", 0, 5, out records));
            Assert.AreEqual(5, records.Length);

            for (int i = 0; i < _prepares2.Length; i++)
            {
                Assert.AreEqual(_prepares2[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_2_range_on_from_start_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("CD", 0, 2, out records));
            Assert.AreEqual(2, records.Length);

            Assert.AreEqual(_prepares2[0], records[0]);
            Assert.AreEqual(_prepares2[1], records[1]);
        }

        [Test]
        public void return_correct_2_2_range_on_from_start_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("CD", 2, 2, out records));
            Assert.AreEqual(2, records.Length);

            Assert.AreEqual(_prepares2[2], records[0]);
            Assert.AreEqual(_prepares2[3], records[1]);
        }

        [Test]
        public void return_empty_range_for_5_1_range_on_from_start_range_query_request_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("CD", 5, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_second_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 0, 5, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_5_1_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_second_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 5, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_second_stream_with_from_end_vesion()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", -1, 5, out records));
            Assert.AreEqual(5, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares2.Length; i++)
            {
                Assert.AreEqual(_prepares2[i], records[i]);
            }
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_second_stream_with_specific_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", 4, 5, out records));
            Assert.AreEqual(5, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares2.Length; i++)
            {
                Assert.AreEqual(_prepares2[i], records[i]);
            }
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_second_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", -1, 5, out records));
            Assert.AreEqual(5, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares2.Length; i++)
            {
                Assert.AreEqual(_prepares2[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_1_range_on_from_end_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", 0, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares2[0], records[0]);
        }

        [Test]
        public void return_correct_from_end_1_range_on_from_end_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", -1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares2[4], records[0]);
        }

        [Test]
        public void return_correct_1_1_range_on_from_end_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", 1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares2[1], records[0]);
        }

        [Test]
        public void return_correct_from_end_2_range_on_from_end_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", -1, 2, out records));
            Assert.AreEqual(2, records.Length);

            Assert.AreEqual(_prepares2[4], records[0]);
            Assert.AreEqual(_prepares2[3], records[1]);
        }

        [Test]
        public void return_empty_range_for_5_1_range_on_from_end_range_query_request_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", 5, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_second_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 0, 5, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_5_1_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_second_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 5, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        #endregion

        #region third

        [Test]
        public void return_correct_last_event_version_for_third_stream()
        {
            Assert.AreEqual(6, ReadIndex.GetLastStreamEventNumber("EF"));
        }

        [Test]
        public void return_minus_one_when_aked_for_last_version_for_stream_with_same_hash_as_third()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("FY"));
        }

        [Test]
        public void return_correct_first_record_for_third_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("EF", 0, out record));
            Assert.AreEqual(_prepares3[0], record);
        }

        [Test]
        public void return_correct_last_log_record_for_third_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("EF", 6, out record));
            Assert.AreEqual(_prepares3[6], record);
        }

        [Test]
        public void not_find_record_with_version_7_in_third_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("EF", 7, out record));
        }

        [Test]
        public void return_not_found_for_record_version_7_for_stream_with_same_hash_as_third_stream()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("FY", 7, out record));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("EF", 0, 7, out records));
            Assert.AreEqual(7, records.Length);

            for (int i = 0; i < _prepares3.Length; i++)
            {
                Assert.AreEqual(_prepares3[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_7_range_on_from_start_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("EF", 0, 7, out records));
            Assert.AreEqual(7, records.Length);

            Assert.AreEqual(_prepares3[0], records[0]);
            Assert.AreEqual(_prepares3[1], records[1]);
            Assert.AreEqual(_prepares3[2], records[2]);
            Assert.AreEqual(_prepares3[3], records[3]);
            Assert.AreEqual(_prepares3[4], records[4]);
            Assert.AreEqual(_prepares3[5], records[5]);
            Assert.AreEqual(_prepares3[6], records[6]);
        }

        [Test]
        public void return_correct_2_3_range_on_from_start_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("EF", 2, 3, out records));
            Assert.AreEqual(3, records.Length);

            Assert.AreEqual(_prepares3[2], records[0]);
            Assert.AreEqual(_prepares3[3], records[1]);
            Assert.AreEqual(_prepares3[4], records[2]);
        }

        [Test]
        public void return_empty_range_for_7_1_range_on_from_start_range_query_request_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("EF", 7, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_third_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsForward("FY", 0, 7, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_7_1_range_on_from_start_range_query_for_non_existing_stream_with_same_hash_as_third_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("EF", 7, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_third_stream_from_specific_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", 6, 7, out records));
            Assert.AreEqual(7, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares3.Length; i++)
            {
                Assert.AreEqual(_prepares3[i], records[i]);
            }
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_third_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", -1, 7, out records));
            Assert.AreEqual(7, records.Length);

            records = records.Reverse().ToArray();

            for (int i = 0; i < _prepares3.Length; i++)
            {
                Assert.AreEqual(_prepares3[i], records[i]);
            }
        }

        [Test]
        public void return_correct_0_1_range_on_from_end_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", 0, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares3[0], records[0]);
        }

        [Test]
        public void return_correct_from_end_1_range_on_from_end_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", -1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares3[6], records[0]);
        }

        [Test]
        public void return_correct_1_1_range_on_from_end_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", 1, 1, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_prepares3[1], records[0]);
        }

        [Test]
        public void return_correct_from_end_2_range_on_from_end_range_query_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", -1, 2, out records));
            Assert.AreEqual(2, records.Length);

            Assert.AreEqual(_prepares3[6], records[0]);
            Assert.AreEqual(_prepares3[5], records[1]);
        }

        [Test]
        public void return_empty_range_for_7_1_range_on_from_end_range_query_request_for_third_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", 7, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_third_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("FY", 0, 7, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_7_1_range_on_from_end_range_query_for_non_existing_stream_with_same_hash_as_third_one()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("EF", 7, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        #endregion
    }
}
