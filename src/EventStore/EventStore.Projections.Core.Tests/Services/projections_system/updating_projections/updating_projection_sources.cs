﻿// Copyright (c) 2012, Event Store LLP
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

using System;
using System.Collections.Generic;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using System.Linq;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Tests.Services.projections_system.updating_projections
{
    namespace updating_projection_sources
    {
        public abstract class with_updated_projection : with_projection_config
        {
            private ProjectionManagementMessage.Statistics _allStatistics;
            protected ProjectionManagementMessage.ProjectionState _state;
            private ProjectionManagementMessage.ProjectionQuery _query;
            private ProjectionStatistics _statistics;
            protected JObject _stateData;

            protected abstract string GivenOriginalSource();
            protected abstract string GivenUpdatedSource();

            protected override bool GivenStartSystemProjections()
            {
                return true;
            }

            protected override IEnumerable<WhenStep> When()
            {
                yield return CreateWriteEvent("stream1", "type1", "{\"Data\": 1}");
                yield return
                    new ProjectionManagementMessage.Post(
                        Envelope, ProjectionMode.Continuous, _projectionName,
                        ProjectionManagementMessage.RunAs.System, "js", GivenOriginalSource(), true,
                        _checkpointsEnabled, _emitEnabled);
                yield return CreateWriteEvent("stream1", "type2", "{\"Data\": 2}");
                yield return CreateWriteEvent("stream2", "type2", "{\"Data\": 3}");
                yield return CreateWriteEvent("stream3", "type3", "{\"Data\": 4}");
                yield return CreateWriteEvent("stream3", "type1", "{\"Data\": 5}");
                yield return
                    new ProjectionManagementMessage.Disable(
                        Envelope, _projectionName, ProjectionManagementMessage.RunAs.System);
                yield return
                    new ProjectionManagementMessage.UpdateQuery(
                        Envelope, _projectionName, ProjectionManagementMessage.RunAs.System, "js",
                        GivenUpdatedSource(), _emitEnabled);
                yield return CreateWriteEvent("stream2", "type3", "{\"Data\": 6}");
                yield return CreateWriteEvent("stream3", "type4", "{\"Data\": 7}");
                yield return
                    new ProjectionManagementMessage.Enable(
                        Envelope, _projectionName, ProjectionManagementMessage.RunAs.System);
                yield return CreateWriteEvent("stream3", "type4", "{\"Data\": 8}");
                yield return CreateWriteEvent("stream4", "type5", "{\"Data\": 9}");
                yield return CreateWriteEvent("stream5", "type1", "{\"Data\": 10}");
                yield return
                    new ProjectionManagementMessage.GetStatistics(
                        Envelope, ProjectionMode.AllNonTransient, _projectionName, false);
                yield return new ProjectionManagementMessage.GetState(Envelope, _projectionName, "");
                yield return
                    new ProjectionManagementMessage.GetQuery(
                        Envelope, _projectionName, ProjectionManagementMessage.RunAs.Anonymous);

                _allStatistics = HandledMessages.OfType<ProjectionManagementMessage.Statistics>().LastOrDefault();
                _statistics = _allStatistics != null ? _allStatistics.Projections.SingleOrDefault() : null;

                _state = HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().LastOrDefault();
                _stateData = _state != null ? EatException(() => _state.State.ParseJson<JObject>()) : null;
                _query = HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().LastOrDefault();

            }

            [Test]
            public void status_is_running()
            {
                Assert.NotNull(_statistics);
                Assert.AreEqual("Running", _statistics.Status);
            }

            [Test]
            public void query_test_is_updated()
            {
                Assert.NotNull(_query);
                Assert.AreEqual(GivenUpdatedSource(), _query.Query);
            }

            [Test]
            public void projection_state_can_be_retrieved()
            {
                Assert.NotNull(_state);
                Assert.NotNull(_stateData);
                Console.WriteLine(_stateData.ToJson());
            }
        }

        [TestFixture]
        public class when_adding_an_event_type : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type1: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type1: handle,
                        type3: handle,
                    });
                ";
            }

            [Test]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 5, 6}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                var pos = GetTfPos("stream5", 0);
                Assert.AreEqual(
                    CheckpointTag.FromEventTypeIndexPositions(0, pos, new Dictionary<string, int> {{"type1", 1}, {"type3", 1}}), _state.Position);
            }

        }

        [TestFixture]
        public class when_replacing_an_event_type : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type1: handle,
                        type2: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type1: handle,
                        type3: handle,
                    });
                ";

            }

            [Test]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 3, 5, 6, 10}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                var pos = GetTfPos("stream5", 0);
                Assert.AreEqual(
                    CheckpointTag.FromEventTypeIndexPositions(0, pos, new Dictionary<string, int> {{"type1", 1}, {"type3", 1}}), _state.Position);
            }
        }

        [TestFixture]
        public class when_replacing_any_with_an_event_type : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type3: handle,
                    });
                ";
            }

            [Test]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 3, 4, 5, 6}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                var pos = GetTfPos("stream2", 1);
                Assert.That(
                    CheckpointTag.FromEventTypeIndexPositions(0, pos, new Dictionary<string, int> {{"type3", 1}}) <= _state.Position);
            }
        }

        [TestFixture]
        public class when_replacing_specific_event_types_with_any : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        type1: handle,
                        type2: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromAll().when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            [Test]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 3, 5, 6, 7, 8, 9, 10}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                var pos = GetTfPos("stream5", 0);
                Assert.AreEqual(CheckpointTag.FromPosition(0, pos.CommitPosition, pos.PreparePosition), _state.Position);
            }
        }

        [TestFixture]
        public class when_replacing_stream_with_multiple_streams : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStream('stream1').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStreams('stream1', 'stream2').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            [Test, Ignore("No position with stream tag yet")]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 6}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                Assert.AreEqual(
                    CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"stream1", 1}, {"stream2", 1}}),
                    _state.Position);
            }
        }

        [TestFixture]
        public class when_replacing_multiple_streams_with_one_of_them : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStreams('stream1', 'stream2').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStream('stream2').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            [Test]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 3, 6}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                Assert.AreEqual(CheckpointTag.FromStreamPosition(0, "stream2", 1), _state.Position);
            }
        }

        [TestFixture]
        public class when_replacing_a_stream_in_multiple_streams : with_updated_projection
        {
            protected override string GivenOriginalSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStreams('stream1', 'stream2').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            protected override string GivenUpdatedSource()
            {
                return @"
                    function handle(s, e) { if (e.data && e.data.Data) s.d.push(e.data.Data); return s; }
                    fromStreams('stream2', 'stream3').when({
                        $init: function(){return {d:[]};},
                        $any: handle,
                    });
                ";
            }

            [Test, Ignore("No position in multi-stream tag")]
            public void correct_event_sequence_has_been_processed()
            {
                HelperExtensions.AssertJson(new {d = new[] {1, 2, 3, 6, 7, 8}}, _stateData);
            }

            [Test]
            public void projection_position_is_correct()
            {
                Assert.AreEqual(
                    CheckpointTag.FromStreamPositions(0, new Dictionary<string, int> {{"stream2", 1}, {"stream3", 3}}),
                    _state.Position);
            }
        }

    }
}
    
