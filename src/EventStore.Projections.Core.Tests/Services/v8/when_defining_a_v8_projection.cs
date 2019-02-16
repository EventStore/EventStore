using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.v8 {
	public static class when_defining_a_v8_projection {
		[TestFixture]
		public class with_from_all_source : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                   fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.AllStreams);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_stream : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStream('stream1').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_multiple_from_streams : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreams(['stream1', 'stream2', 'stream3']).when({
                    $any: function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_multiple_from_streams_plain : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreams('stream1', 'stream2', 'stream3').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_multiple_from_categories : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromCategories(['category1', 'category2', 'category3']).when({
                    $any: function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_multiple_from_categories_plain : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromCategories('category1', 'category2', 'category3').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_from_category : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromCategory('category1').when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_from_category_by_stream : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromCategory('category1').foreachStream().when({
                        $any:function(state, event) {
                            return state;
                        }});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
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
		public class with_from_stream_catalog : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreamCatalog('catalog1')
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual("catalog1", _source.CatalogStream);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.AreEqual(false, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_stream_catalog_with_transform : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreamCatalog('catalog1', function(e) {return e.bodyRaw;})
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesCatalogTransform);
			}
		}

		[TestFixture]
		public class with_from_stream_catalog_by_stream : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreamCatalog('catalog1').foreachStream().when({$any: function(s,e){return e;}})
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.AreEqual("catalog1", _source.CatalogStream);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);
				Assert.AreEqual(true, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_streams_matching : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromStreamsMatching(function(sm){return true;})
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.AllStreams);
				Assert.That(_source.Categories == null || _source.Categories.Length == 0);
				Assert.That(_source.Streams == null || _source.Streams.Length == 0);

				Assert.AreEqual("$all", _source.CatalogStream);
				Assert.AreEqual(true, _source.DefinesCatalogTransform);
				Assert.AreEqual(true, _source.ByStreams);
			}
		}

		[TestFixture]
		public class with_from_all_by_custom_partitions : TestFixtureWithJsProjection {
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
		public class with_output_to : TestFixtureWithJsProjection {
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
		public class with_transform_by : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    fromAll().when({
                        $any:function(state, event) {
                            return state;
                        }}).transformBy(function(s) {return s;});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesStateTransform);
			}
		}

		public class with_filter_by : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesStateTransform);
			}
		}

		public class with_output_state : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll().outputState();
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.ProducesResults);
			}
		}

		public class without_when : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll();
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(false, _source.DefinesFold);
			}
		}

		public class with_when : TestFixtureWithJsProjection {
			protected override void Given() {
				_projection = @"
                    var z = fromAll().when({a: function(s,e) {}});
                ";
				_state = @"{""count"": 0}";
			}

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.DefinesFold);
			}
		}

		[TestFixture]
		public class with_state_stream_name_option : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual("state-stream", _source.ResultStreamNameOption);
			}
		}

		[TestFixture]
		public class with_include_links_option : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.IncludeLinksOption);
			}
		}

		[TestFixture]
		public class with_bi_state_option : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(true, _source.IsBiState);
			}
		}

		[TestFixture]
		public class with_reorder_events_option : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(500, _source.ProcessingLagOption);
				Assert.AreEqual(true, _source.ReorderEventsOption);
			}
		}

		[TestFixture]
		public class with_multiple_option_statements : TestFixtureWithJsProjection {
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

			[Test, Category("v8")]
			public void source_definition_is_correct() {
				Assert.AreEqual(500, _source.ProcessingLagOption);
				Assert.AreEqual(true, _source.ReorderEventsOption);
			}
		}
	}

	[TestFixture]
	public class with_foreach_and_deleted_notification_handled : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"fromAll().foreachStream().when({
                $deleted: function(){}
            })";
			_state = @"{}";
		}

		[Test]
		public void source_definition_is_correct() {
			Assert.AreEqual(true, _source.HandlesDeletedNotifications);
		}
	}

	[TestFixture]
	public class with_deleted_notification_handled : TestFixtureWithJsProjection {
		protected override void Given() {
			_projection = @"fromAll().foreachStream().when({
                $deleted: function(){}
            })";
			_state = @"{}";
		}

		[Test]
		public void source_definition_is_correct() {
			Assert.AreEqual(true, _source.HandlesDeletedNotifications);
		}
	}

	public abstract class specification_with_event_handled : TestFixtureWithJsProjection {
		protected ResolvedEvent _handledEvent;
		protected string _newState;
		protected string _newSharedState;
		protected EmittedEventEnvelope[] _emittedEventEnvelopes;

		protected override void When() {
			_stateHandler.ProcessEvent(
				"",
				CheckpointTag.FromPosition(
					0, _handledEvent.Position.CommitPosition, _handledEvent.Position.PreparePosition), "",
				_handledEvent,
				out _newState, out _newSharedState, out _emittedEventEnvelopes);
		}

		protected static ResolvedEvent CreateSampleEvent(
			string streamId, int sequenceNumber, string eventType, string data, TFPos tfPos) {
			return new ResolvedEvent(
				streamId, sequenceNumber, streamId, sequenceNumber, true, tfPos, Guid.NewGuid(), eventType, true, data,
				"{}", "{\"position_meta\":1}");
		}
	}

	[TestFixture]
	public class with_no_when_statement : specification_with_event_handled {
		protected override void Given() {
			_projection = @"fromAll();";
			_state = @"{}";
			_handledEvent = CreateSampleEvent("stream", 0, "event_type", "{\"data\":1}", new TFPos(100, 50));
		}

		[Test]
		public void returns_event_data_as_state() {
			Assert.AreEqual("{\"data\":1}", _newState);
			Assert.IsTrue(_emittedEventEnvelopes == null || !_emittedEventEnvelopes.Any());
		}
	}

	[TestFixture]
	public class with_return_link_metadata : specification_with_event_handled {
		protected override void Given() {
			_projection = @"fromAll().when({$any:function(s,e){
                return e.linkMetadata;
            }})";
			_state = @"{}";
			_handledEvent = CreateSampleEvent("stream", 0, "event_type", "{\"data\":1}", new TFPos(100, 50));
		}

		[Test]
		public void returns_position_metadata_as_state() {
			Assert.AreEqual("{\"position_meta\":1}", _newState);
			Assert.IsTrue(_emittedEventEnvelopes == null || !_emittedEventEnvelopes.Any());
		}
	}
}
