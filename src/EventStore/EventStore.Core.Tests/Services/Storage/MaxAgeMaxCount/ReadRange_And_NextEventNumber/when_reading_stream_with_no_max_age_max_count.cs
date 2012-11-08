using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.ReadRange_And_NextEventNumber
{
    class when_reading_stream_with_no_max_age_max_count : ReadIndexTestScenario
    {

        protected override void WriteTestScenario()
        {
            WriteStreamCreated("ES");
            WriteSingleEvent("ES", 1, "bla");
            WriteSingleEvent("ES", 2, "bla");
            WriteSingleEvent("ES", 3, "bla");
            WriteSingleEvent("ES", 4, "bla");
            WriteSingleEvent("ES", 5, "bla");
        }

        [Test]
        public void on_read_forward_from_start_to_middle_next_event_number_is_middle_plus_1_and_its_not_end_of_stream()
        {
        }

        [Test]
        public void on_read_forward_from_the_middle_to_end_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
        }

        [Test]
        public void on_read_forward_from_the_middle_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
        }

        [Test]
        public void on_read_forward_from_the_out_of_bounds_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
        }


        [Test]
        public void on_read_backward_from_the_end_to_middle_next_event_number_is_middle_minus_1_and_its_not_end_of_stream()
        {
        }

        [Test]
        public void on_read_backward_from_middle_to_start_next_event_number_is_minus_1_and_its_end_of_stream()
        {
        }

        [Test]
        public void on_read_backward_from_middle_to_before_start_next_event_number_is_minus_1_and_its_end_of_stream()
        {
        }

        [Test]
        public void on_read_backward_from_out_of_bounds_to_middle_next_event_number_is_middle_minus_1_and_its_not_end_of_stream()
        {
        }

        [Test]
        public void on_read_backward_from_out_of_bounds_to_out_of_bounds_next_event_number_is_end_and_its_not_end_of_stream()
        {
        }
    }
}
