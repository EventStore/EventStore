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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    internal class transaction
    {
        [Test]
        [Category("Network")]
        public void should_start_on_non_existing_stream_with_correct_exp_ver_and_create_stream_on_commit()
        {
            const string stream = "should_start_on_non_existing_stream_with_correct_exp_ver_and_create_stream_on_commit";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.NoStream);
                Assert.DoesNotThrow(start.Wait);
                var write = store.TransactionalWriteAsync(start.Result.TransactionId, stream, new[] {new TestEvent()});
                Assert.DoesNotThrow(write.Wait);
                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.DoesNotThrow(commit.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void should_start_on_non_existing_stream_with_exp_ver_any_and_create_stream_on_commit()
        {
            const string stream = "should_start_on_non_existing_stream_with_exp_ver_any_and_create_stream_on_commit";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.Any);
                Assert.DoesNotThrow(start.Wait);
                var write = store.TransactionalWriteAsync(start.Result.TransactionId, stream, new[] {new TestEvent()});
                Assert.DoesNotThrow(write.Wait);
                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.DoesNotThrow(commit.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_to_commit_non_existing_stream_with_wrong_exp_ver()
        {
            const string stream = "should_fail_to_commit_non_existing_stream_with_wrong_exp_ver";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(start.Wait);
                var write = store.TransactionalWriteAsync(start.Result.TransactionId, stream, new[] { new TestEvent() });
                Assert.DoesNotThrow(write.Wait);
                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.That(() => commit.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_create_stream_if_commits_no_events_to_empty_stream()
        {
            const string stream = "should_create_stream_if_commits_no_events_to_empty_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.NoStream);
                Assert.DoesNotThrow(start.Wait);
                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.DoesNotThrow(commit.Wait);

                var streamCreated = store.ReadEventStreamForwardAsync(stream, 0, 1);
                Assert.DoesNotThrow(streamCreated.Wait);

                Assert.That(streamCreated.Result.Events.Length, Is.EqualTo(1));//stream created event
            }
        }

        [Test]
        [Category("Network")]
        public void should_validate_expectations_on_commit()
        {
            const string stream = "should_validate_expectations_on_commit";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, 100500);
                Assert.DoesNotThrow(start.Wait);
                var write = store.TransactionalWriteAsync(start.Result.TransactionId, stream, new[] { new TestEvent() });
                Assert.DoesNotThrow(write.Wait);
                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.That(() => commit.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_commit_when_writing_with_exp_ver_any_even_while_somene_is_writing_in_parallel()
        {
            const string stream = "should_commit_when_writing_with_exp_ver_any_even_while_somene_is_writing_in_parallel";

            var transWritesCompleted = new AutoResetEvent(false);
            var writesToSameStreamCompleted = new AutoResetEvent(false);

            var totalTranWrites = 500;
            var totalPlainWrites = 500;

            //excplicitly creating stream
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                store.CreateStream(stream, false, new byte[0]);
            }

            //500 events during transaction
            ThreadPool.QueueUserWorkItem(_ =>
            {
                using (var store = EventStoreConnection.Create())
                {
                    store.Connect(MiniNode.Instance.TcpEndPoint);
                    var transaction = store.StartTransaction(stream, ExpectedVersion.Any);
                    var writes = new List<Task>();
                    for (int i = 0; i < totalTranWrites; i++)
                    {
                        if (i % 10 == 0)
                            writes.RemoveAll(write => write.IsCompleted);

                        writes.Add(store.TransactionalWriteAsync(transaction.TransactionId,
                                                                 transaction.Stream,
                                                                 new[] { new TestEvent((i + 1).ToString(), "trans write") }));
                    }

                    Task.WaitAll(writes.ToArray());
                    store.CommitTransaction(transaction.TransactionId, transaction.Stream);

                    transWritesCompleted.Set();
                }
            });

            //500 events to same stream in parallel
            ThreadPool.QueueUserWorkItem(_ =>
            {
                using (var store = EventStoreConnection.Create())
                {
                    store.Connect(MiniNode.Instance.TcpEndPoint);
                    var writes = new List<Task>();
                    for (int i = 0; i < totalPlainWrites; i++)
                    {
                        if (i % 10 == 0)
                            writes.RemoveAll(write => write.IsCompleted);

                        writes.Add(store.AppendToStreamAsync(stream,
                                                             ExpectedVersion.Any,
                                                             new[] {new TestEvent((i + 1).ToString(), "plain write")}));
                    }

                    Task.WaitAll(writes.ToArray());

                    writesToSameStreamCompleted.Set();
                }
            });

            transWritesCompleted.WaitOne();
            writesToSameStreamCompleted.WaitOne();

            //check all written
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var slice = store.ReadEventStreamForward(stream, 0, totalTranWrites + totalPlainWrites + 1);
                Assert.That(slice.Events.Length, Is.EqualTo(totalTranWrites + totalPlainWrites + 1));

                Assert.That(slice.Events.Count(ent => Encoding.UTF8.GetString(ent.Metadata) == "trans write"), Is.EqualTo(totalTranWrites));
                Assert.That(slice.Events.Count(ent => Encoding.UTF8.GetString(ent.Metadata) == "plain write"), Is.EqualTo(totalPlainWrites));
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_to_commit_if_started_with_correct_ver_but_committing_with_bad()
        {
            const string stream = "should_fail_to_commit_if_started_with_correct_ver_but_committing_with_bad";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                store.CreateStream(stream, false, new byte[0]);
            }

            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(start.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] {new TestEvent()});
                Assert.DoesNotThrow(append.Wait);

                var write = store.TransactionalWriteAsync(start.Result.TransactionId, start.Result.Stream, new[] { new TestEvent() });
                Assert.DoesNotThrow(write.Wait);

                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.That(() => commit.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<WrongExpectedVersionException>());
            }
        }

        [Test]
        [Category("Network")]
        public void should_not_fail_to_commit_if_started_with_wrong_ver_but_committing_with_correct_ver()
        {
            const string stream = "should_not_fail_to_commit_if_started_with_wrong_ver_but_committing_with_correct_ver";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                store.CreateStream(stream, false, new byte[0]);
            }

            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, 1);
                Assert.DoesNotThrow(start.Wait);

                var append = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, new[] { new TestEvent() });
                Assert.DoesNotThrow(append.Wait);

                var write = store.TransactionalWriteAsync(start.Result.TransactionId, start.Result.Stream, new[] { new TestEvent() });
                Assert.DoesNotThrow(write.Wait);

                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.DoesNotThrow(commit.Wait);
            }
        }

        [Test]
        [Category("Network")]
        public void should_fail_to_commit_if_started_with_correct_ver_but_on_commit_stream_was_deleted()
        {
            const string stream = "should_fail_to_commit_if_started_with_correct_ver_but_on_commit_stream_was_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                store.CreateStream(stream, false, new byte[0]);
            }

            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var start = store.StartTransactionAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(start.Wait);

                var write = store.TransactionalWriteAsync(start.Result.TransactionId, start.Result.Stream, new[] { new TestEvent() });
                Assert.DoesNotThrow(write.Wait);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.DoesNotThrow(delete.Wait);

                var commit = store.CommitTransactionAsync(start.Result.TransactionId, start.Result.Stream);
                Assert.That(() => commit.Wait(), Throws.Exception.TypeOf<AggregateException>().With.InnerException.TypeOf<StreamDeletedException>());
            }
        }
    }
}
