using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Common.Utils;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class transaction : SpecificationWithDirectoryPerTestFixture {
		private MiniNode _node;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_node = new MiniNode(PathName);
			_node.Start();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_node.Shutdown();
			base.TestFixtureTearDown();
		}

		protected virtual IEventStoreConnection BuildConnection(MiniNode node) {
			return TestConnection.Create(node.TcpEndPoint);
		}

		[Test]
		[Category("Network")]
		public void should_start_on_non_existing_stream_with_correct_exp_ver_and_create_stream_on_commit() {
			const string stream =
				"should_start_on_non_existing_stream_with_correct_exp_ver_and_create_stream_on_commit";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.NoStream).Result) {
					transaction.WriteAsync(new[] {TestEvent.NewTestEvent()}).Wait();
					Assert.AreEqual(0, transaction.CommitAsync().Result.NextExpectedVersion);
				}
			}
		}

		[Test]
		[Category("Network")]
		public void should_start_on_non_existing_stream_with_exp_ver_any_and_create_stream_on_commit() {
			const string stream = "should_start_on_non_existing_stream_with_exp_ver_any_and_create_stream_on_commit";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.Any).Result) {
					transaction.WriteAsync(new[] {TestEvent.NewTestEvent()}).Wait();
					Assert.AreEqual(0, transaction.CommitAsync().Result.NextExpectedVersion);
				}
			}
		}

		[Test]
		[Category("Network")]
		public void should_fail_to_commit_non_existing_stream_with_wrong_exp_ver() {
			const string stream = "should_fail_to_commit_non_existing_stream_with_wrong_exp_ver";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, 1).Result) {
					transaction.WriteAsync(TestEvent.NewTestEvent()).Wait();
					Assert.That(() => transaction.CommitAsync().Wait(),
						Throws.Exception.TypeOf<AggregateException>()
							.With.InnerException.TypeOf<WrongExpectedVersionException>());
				}
			}
		}

		[Test]
		[Category("Network")]
		public void should_do_nothing_if_commits_no_events_to_empty_stream() {
			const string stream = "should_do_nothing_if_commits_no_events_to_empty_stream";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.NoStream).Result) {
					Assert.AreEqual(-1, transaction.CommitAsync().Result.NextExpectedVersion);
				}

				var result = store.ReadStreamEventsForwardAsync(stream, 0, 1, resolveLinkTos: false).Result;
				Assert.That(result.Events.Length, Is.EqualTo(0));
			}
		}

		[Test, Category("Network")]
		public void should_do_nothing_if_transactionally_writing_no_events_to_empty_stream() {
			const string stream = "should_do_nothing_if_transactionally_writing_no_events_to_empty_stream";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.NoStream).Result) {
					Assert.DoesNotThrow(() => transaction.WriteAsync().Wait());
					Assert.AreEqual(-1, transaction.CommitAsync().Result.NextExpectedVersion);
				}

				var result = store.ReadStreamEventsForwardAsync(stream, 0, 1, resolveLinkTos: false).Result;
				Assert.That(result.Events.Length, Is.EqualTo(0));
			}
		}

		[Test]
		[Category("Network")]
		public void should_validate_expectations_on_commit() {
			const string stream = "should_validate_expectations_on_commit";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, 100500).Result) {
					transaction.WriteAsync(TestEvent.NewTestEvent());
					Assert.That(() => transaction.CommitAsync().Wait(),
						Throws.Exception.TypeOf<AggregateException>().With.InnerException
							.TypeOf<WrongExpectedVersionException>());
				}
			}
		}

		[Test]
		[Category("Network")]
		public void should_commit_when_writing_with_exp_ver_any_even_while_somene_is_writing_in_parallel() {
			const string stream =
				"should_commit_when_writing_with_exp_ver_any_even_while_somene_is_writing_in_parallel";

			var transWritesCompleted = new ManualResetEventSlim(false);
			var writesToSameStreamCompleted = new ManualResetEventSlim(false);

			const int totalTranWrites = 500;
			const int totalPlainWrites = 500;

			//500 events during transaction
			ThreadPool.QueueUserWorkItem(_ => {
				Assert.DoesNotThrow(() => {
					using (var store = BuildConnection(_node)) {
						store.ConnectAsync().Wait();
						using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.Any).Result) {
							var writes = new List<Task>();
							for (int i = 0; i < totalTranWrites; i++) {
								writes.Add(transaction.WriteAsync(TestEvent.NewTestEvent(i.ToString(), "trans write")));
							}

							Task.WaitAll(writes.ToArray());
							transaction.CommitAsync().Wait();
							transWritesCompleted.Set();
						}
					}
				});
			});

			//500 events to same stream in parallel
			ThreadPool.QueueUserWorkItem(_ => {
				Assert.DoesNotThrow(() => {
					using (var store = BuildConnection(_node)) {
						store.ConnectAsync().Wait();
						var writes = new List<Task>();
						for (int i = 0; i < totalPlainWrites; i++) {
							writes.Add(store.AppendToStreamAsync(stream,
								ExpectedVersion.Any,
								new[] {TestEvent.NewTestEvent(i.ToString(), "plain write")}));
						}

						Task.WaitAll(writes.ToArray());
						writesToSameStreamCompleted.Set();
					}
				});
			});

			transWritesCompleted.Wait();
			writesToSameStreamCompleted.Wait();

			// check all written
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				var slice = store.ReadStreamEventsForwardAsync(stream, 0, totalTranWrites + totalPlainWrites, false)
					.Result;
				Assert.That(slice.Events.Length, Is.EqualTo(totalTranWrites + totalPlainWrites));

				Assert.That(slice.Events.Count(ent => Helper.UTF8NoBom.GetString(ent.Event.Metadata) == "trans write"),
					Is.EqualTo(totalTranWrites));
				Assert.That(slice.Events.Count(ent => Helper.UTF8NoBom.GetString(ent.Event.Metadata) == "plain write"),
					Is.EqualTo(totalPlainWrites));
			}
		}

		[Test]
		[Category("Network")]
		public void should_fail_to_commit_if_started_with_correct_ver_but_committing_with_bad() {
			const string stream = "should_fail_to_commit_if_started_with_correct_ver_but_committing_with_bad";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.EmptyStream).Result) {
					store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] {TestEvent.NewTestEvent()})
						.Wait();
					transaction.WriteAsync(TestEvent.NewTestEvent()).Wait();
					Assert.That(() => transaction.CommitAsync().Wait(),
						Throws.Exception.TypeOf<AggregateException>().With.InnerException
							.TypeOf<WrongExpectedVersionException>());
				}
			}
		}

		[Test, Category("Network")]
		public void should_not_fail_to_commit_if_started_with_wrong_ver_but_committing_with_correct_ver() {
			const string stream = "should_not_fail_to_commit_if_started_with_wrong_ver_but_committing_with_correct_ver";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, 0).Result) {
					store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] {TestEvent.NewTestEvent()})
						.Wait();
					transaction.WriteAsync(TestEvent.NewTestEvent()).Wait();
					Assert.AreEqual(1, transaction.CommitAsync().Result.NextExpectedVersion);
				}
			}
		}

		[Test]
		[Category("Network")]
		public void should_fail_to_commit_if_started_with_correct_ver_but_on_commit_stream_was_deleted() {
			const string stream = "should_fail_to_commit_if_started_with_correct_ver_but_on_commit_stream_was_deleted";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();
				using (var transaction = store.StartTransactionAsync(stream, ExpectedVersion.EmptyStream).Result) {
					transaction.WriteAsync(TestEvent.NewTestEvent()).Wait();
					store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream, hardDelete: true).Wait();
					Assert.That(() => transaction.CommitAsync().Wait(),
						Throws.Exception.TypeOf<AggregateException>().With.InnerException
							.TypeOf<StreamDeletedException>());
				}
			}
		}

		[Test, Category("LongRunning")]
		public void idempotency_is_correct_for_explicit_transactions_with_expected_version_any() {
			const string streamId = "idempotency_is_correct_for_explicit_transactions_with_expected_version_any";
			using (var store = BuildConnection(_node)) {
				store.ConnectAsync().Wait();

				var e = new EventData(Guid.NewGuid(), "SomethingHappened", true,
					Helper.UTF8NoBom.GetBytes("{Value:42}"), null);

				var transaction1 = store.StartTransactionAsync(streamId, ExpectedVersion.Any).Result;
				transaction1.WriteAsync(new[] {e}).Wait();
				Assert.AreEqual(0, transaction1.CommitAsync().Result.NextExpectedVersion);

				var transaction2 = store.StartTransactionAsync(streamId, ExpectedVersion.Any).Result;
				transaction2.WriteAsync(new[] {e}).Wait();
				Assert.AreEqual(0, transaction2.CommitAsync().Result.NextExpectedVersion);

				var res = store.ReadStreamEventsForwardAsync(streamId, 0, 100, false).Result;
				Assert.AreEqual(1, res.Events.Length);
				Assert.AreEqual(e.EventId, res.Events[0].Event.EventId);
			}
		}
	}
}
