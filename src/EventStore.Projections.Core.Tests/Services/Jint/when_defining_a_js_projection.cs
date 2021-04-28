using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.Jint
{
	[TestFixture]
	public class when_defining_a_js_projection {
		private const string _projectionType = "INTERPRETED";
		[TestFixture]
		public class with_from_all_source : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                   fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.AllStreams);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_stream : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromStream('stream1').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Streams);
				Assert.AreEqual(1, _source.Streams.Length);
				Assert.AreEqual("stream1", _source.Streams[0]);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_multiple_from_streams : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromStreams(['stream1', 'stream2', 'stream3']).when({
                    $any: function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Streams);
				Assert.AreEqual(3, _source.Streams.Length);
				Assert.AreEqual("stream1", _source.Streams[0]);
				Assert.AreEqual("stream2", _source.Streams[1]);
				Assert.AreEqual("stream3", _source.Streams[2]);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_multiple_from_streams_plain : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromStreams('stream1', 'stream2', 'stream3').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Streams);
				Assert.AreEqual(3, _source.Streams.Length);
				Assert.AreEqual("stream1", _source.Streams[0]);
				Assert.AreEqual("stream2", _source.Streams[1]);
				Assert.AreEqual("stream3", _source.Streams[2]);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_multiple_from_categories : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromCategories(['category1', 'category2', 'category3']).when({
                    $any: function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Streams);
				Assert.AreEqual(3, _source.Streams.Length);
				Assert.AreEqual("$ce-category1", _source.Streams[0]);
				Assert.AreEqual("$ce-category2", _source.Streams[1]);
				Assert.AreEqual("$ce-category3", _source.Streams[2]);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_multiple_from_categories_plain : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromCategories('category1', 'category2', 'category3').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Streams);
				Assert.AreEqual(3, _source.Streams.Length);
				Assert.AreEqual("$ce-category1", _source.Streams[0]);
				Assert.AreEqual("$ce-category2", _source.Streams[1]);
				Assert.AreEqual("$ce-category3", _source.Streams[2]);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_category : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromCategory('category1').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Categories);
				Assert.AreEqual(1, _source.Categories.Length);
				Assert.AreEqual("category1", _source.Categories[0]);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_category_by_stream : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromCategory('category1').foreachStream().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.IsNotNull(_source.Categories);
				Assert.AreEqual(1, _source.Categories.Length);
				Assert.AreEqual("category1", _source.Categories[0]);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.AreEqual(true, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_all_by_custom_partitions : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromAll().partitionBy(function(event){
                        return event.eventType;
                    }).when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.AllStreams);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.AreEqual(true, _source.ByCustomPartitions);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_output_to : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromAll()
                    .when({
                        $any:function(state, event) {
                            return state;
                        }})
                    .$defines_state_transform();
                ";
				_state = @"{""count"": 0}";
			}

			[Test]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesStateTransform);
			}
		}

		[TestFixture]
		public class with_transform_by : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }}).transformBy(function(s) {return s;});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesStateTransform);
			}
		}

		public class with_filter_by : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    fromAll().when({
                        some: function(state, event) {
                            return state;
                        }
                    }).filterBy(function(s) {return true;});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesStateTransform);
			}
		}

		public class with_output_state : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll().outputState();
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.ProducesResults);
			}
		}

		public class without_when : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll();
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.DefinesFold);
			}
		}

		public class with_when : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll().when({a: function(s,e) {}});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesFold);
			}
		}

		[TestFixture]
		public class with_state_stream_name_option : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    options({
                        resultStreamName: 'state-stream',
                    });
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual("state-stream", _source.ResultStreamNameOption);
			}
		}

		[TestFixture]
		public class with_include_links_option : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    options({
                        $includeLinks: true,
                    });
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.IncludeLinksOption);
			}
		}

		[TestFixture]
		public class with_bi_state_option : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    options({
                        biState: true,
                    });
                    fromAll().when({
                        $any:function(state, sharedState, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.IsBiState);
			}
		}

		[TestFixture]
		public class with_reorder_events_option : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    options({
                        reorderEvents: true,
                        processingLag: 500,
                    });
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(500, _source.ProcessingLagOption);
				Assert.AreEqual(true, _source.ReorderEventsOption);
			}
		}

		[TestFixture]
		public class with_multiple_option_statements : TestFixtureWithInterpretedProjection {
			protected override void Given() {
				_projection = @"
                    options({
                        reorderEvents: false,
                        processingLag: 500,
                    });
                    options({
                        reorderEvents: true,
                    });
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category(_projectionType)]
			public void source_definition_is_correct() {
				Assert.AreEqual(500, _source.ProcessingLagOption);
				Assert.AreEqual(true, _source.ReorderEventsOption);
			}
		}
	}
}