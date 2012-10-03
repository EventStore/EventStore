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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Common.Log;

namespace EventStore.TestClient.Commands
{
    internal class RunTestScenariosProcessor : ICmdProcessor
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<RunTestScenariosProcessor>();
        private IScenario[] _scenarios;

        public string Keyword
        {
            get
            {
                return string.Format("RT");
            }
        }

        public string Usage
        {
            get
            {
                return string.Format("{0} " +
                                     "<max concurrent requests, default = 500> " +
                                     "<threads, default = 20> " +
                                     "<streams, default = 20> " +
                                     "<eventsPerStream, default = 10000> " +
                                     "<streams delete step, default = 7> ",
                                     Keyword);
            }
        }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            context.IsAsync();

            if(args.Length != 0 && args.Length != 5)
                return false;

            var maxConcurrentRequests = 500;
            var threads = 20;
            var streams = 20;
            var eventsPerStream = 10000;
            var streamDeleteStep = 7;

            if(args.Length == 5)
            {
                try
                {
                    maxConcurrentRequests = int.Parse(args[0]);
                    threads = int.Parse(args[1]);
                    streams = int.Parse(args[2]);
                    eventsPerStream = int.Parse(args[3]);
                    streamDeleteStep = int.Parse(args[4]);
                }
                catch (Exception e)
                {
                    Log.Error("Invalid arguments ({0})", e.Message);
                    return false;
                }
            }

            Log.Info("Running scenarios using {0} threads, {1} streams {2} events each, deleting every {3}th stream. Max concurrent ES requests - {4}",
                     threads,
                     streams,
                     eventsPerStream,
                     streamDeleteStep,
                     maxConcurrentRequests);

            _scenarios = new IScenario[] { new Scenario1(maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep) };

            Log.Info("Running test scenarios ({0} total)...", _scenarios.Length);
            foreach (var scenario in _scenarios)
            {
                try
                {
                    scenario.Run();
                    scenario.Clean();
                    Log.Info("Scenario run successfully");
                }
                catch (Exception e)
                {
                    context.Fail(e);
                }
            }
            Log.Info("Finished running test scenarios");

            context.Success();
            return true;
        }
    }

    internal class TestEvent : IEvent
    {
        public Guid EventId { get; private set; }
        public string Type { get; private set; }

        public byte[] Data { get; private set; }
        public byte[] Metadata { get; private set; }

        public TestEvent(int index)
        {
            EventId = Guid.NewGuid();
            Type = index.ToString();

            Data = Encoding.UTF8.GetBytes(string.Format("{0}-{1}", index, new string('#', 1024)));
            Metadata = new byte[0];
        }
    }

    internal enum WriteMode
    {
        SingleEventAtTime,
        Bucket,
        Transactional
    }

    internal interface IScenario
    {
        void Run();
        void Clean();
    }

    internal class Scenario1 : ScenarioBase
    {
        public Scenario1(int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep)
            : base(maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        public override void Run()
        {
            ThreadPool.SetMaxThreads(Threads, Threads);

            KillSingleNodes();
            StartNode();

            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("scenario-stream-{0}", i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            KillSingleNodes();
            StartNode();

            CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            Read(exceptDeleted, from: 0, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

            KillSingleNodes();
        }
    }

    internal abstract class ScenarioBase : IScenario
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Scenario1>();

        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "ES_" + Guid.NewGuid());
        private readonly IPEndPoint _tcpEndPoint = new IPEndPoint(IPAddress.Loopback, 1113);

        protected readonly int MaxConcurrentRequests;
        protected readonly int Threads;
        protected readonly int Streams;
        protected readonly int EventsPerStream;
        protected readonly int StreamDeleteStep;
        protected readonly int Piece;

        protected ScenarioBase(int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep)
        {
            MaxConcurrentRequests = maxConcurrentRequests;
            Threads = threads;
            Streams = streams;
            EventsPerStream = eventsPerStream;
            StreamDeleteStep = streamDeleteStep;
            Piece = 100;
        }

        public abstract void Run();

        public void Clean()
        {
            Log.Info("Deleting {0}...", _dbPath);
            Directory.Delete(_dbPath, true);
            Log.Info("Deleted {0}", _dbPath);
        }

        protected T[][] Split<T>(IEnumerable<T> sequence, int parts)
        {
            var i = 0;
            return sequence.GroupBy(item => i++ % parts).Select(part => part.ToArray()).ToArray();
        }

        protected void Write(WriteMode mode, string[] streams, int eventsPerStream)
        {
            Log.Info("Writing. Mode : {0,-15} Streams : {1,-10} Events per stream : {2,-10}", 
                     mode, 
                     streams.Length, 
                     eventsPerStream);

            var written = new CountdownEvent(streams.Length);

            switch (mode)
            {
                case WriteMode.SingleEventAtTime:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteSingleEventAtTime(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.Bucket:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteBucketOfEventsAtTime(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.Transactional:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteEventsInTransactionalWay(written, streams[i1], eventsPerStream));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }

            written.Wait();

            Log.Info("Finished writing. Mode : {0,-15} Streams : {1,-10} Events per stream : {2,-10}", 
                     mode,
                     streams.Length, 
                     eventsPerStream);
        }

        protected void DeleteStreams(IEnumerable<string> streams)
        {
            Log.Info("Deleting streams...");
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                foreach (var stream in streams)
                {
                    Log.Info("Deleting stream {0}...", stream);
                    store.DeleteStream(stream, EventsPerStream);
                    Log.Info("Stream {0} successfully deleted", stream);
                }
            }
            Log.Info("All streams successfully deleted");
        }

        protected void CheckStreamsDeleted(IEnumerable<string> streams)
        {
            Log.Info("Verifying streams are deleted...");
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                foreach (var stream in streams)
                {
                    try
                    {
                        store.ReadEventStream(stream, 0, 1);
                        throw new ApplicationException(string.Format("Stream {0} should have been deleted!", stream));
                    }
                    catch (Exception e)
                    {
                        Log.Info("Reading from {0} lead to {1}", stream, e.Message);
                        if(e is ApplicationException)
                            throw;
                    }
                }
            }
            Log.Info("Verification succeded");
        }

        protected void Read(string[] streams, int @from, int count)
        {
            Log.Info("Reading [{0}]\nfrom {1,-10} count {2,-10}", string.Join(",", streams), @from, count);
            var read = new CountdownEvent(streams.Length);

            for (int i = 0; i < streams.Length; i++)
            {
                int i1 = i;
                ThreadPool.QueueUserWorkItem(_ => ReadStream(read, streams[i1], @from, count));
            }

            read.Wait();
            Log.Info("Done reading [{0}]", string.Join(",", streams));
        }

        protected void StartNode()
        {
            var clientFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var fileName = Path.Combine(clientFolder, "EventStore.SingleNode.exe");
            var arguments = string.Format("--ip {0} -t {1} -h {2} --db {3}", _tcpEndPoint.Address, _tcpEndPoint.Port, _tcpEndPoint.Port + 1000, _dbPath);

            Log.Info("Starting [{0} {1}]...", fileName, arguments);
            Process.Start(fileName, arguments);
            Thread.Sleep(TimeSpan.FromSeconds(7));
            Log.Info("Started [{0} {1}]", fileName, arguments);
        }

        protected void KillSingleNodes()
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes.Where(p => p.ProcessName == "EventStore.SingleNode"))
            {
                Log.Info("Killing {0}...", process.ProcessName);
                process.Kill();
                while (!process.HasExited) 
                    Thread.Sleep(100);
                Log.Info("Killed {0}", process.ProcessName);
            }
        }

        private void WriteSingleEventAtTime(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}]", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                var tasks = new List<Task>();
                for (var i = 0; i < events; i++)
                {
                    tasks.Add(store.AppendToStreamAsync(stream, i, new[] { new TestEvent(i + 1) }));
                    if (i % 100 == 0)
                        tasks.RemoveAll(t => t.IsCompleted);
                }

                Task.WaitAll(tasks.ToArray());
            }
            Log.Info("Wrote {0} events to [{1}]", events, stream);
            written.Signal();
        }

        private void WriteBucketOfEventsAtTime(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (100 events at once)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                var tasks = new List<Task>();

                var position = 0;
                var bucketSize = 100;

                while (position < events)
                {
                    var bucket = Enumerable.Range(position, bucketSize).Select(x => new TestEvent(x + 1));
                    tasks.Add(store.AppendToStreamAsync(stream, position, bucket));
                    if (position % 100 == 0)
                        tasks.RemoveAll(t => t.IsCompleted);

                    position += bucketSize;
                }

                if(position != events)
                {
                    var start = position - bucketSize;
                    var count = events - start;

                    var bucket = Enumerable.Range(start, count).Select(x => new TestEvent(x + 1));
                    tasks.Add(store.AppendToStreamAsync(stream, start, bucket));
                }

                Task.WaitAll(tasks.ToArray());
            }

            Log.Info("Wrote {0} events to [{1}] (100 events at once)", events, stream);
            written.Signal();
        }

        private void WriteEventsInTransactionalWay(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (in single transaction)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));
                var esTrans = store.StartTransaction(stream, 0);

                var tasks = new List<Task>();
                for (var i = 0; i < events; i++)
                {
                    tasks.Add(store.TransactionalWriteAsync(esTrans.TransactionId, stream, new[] { new TestEvent(i + 1) }));
                    if (i % 100 == 0)
                        tasks.RemoveAll(t => t.IsCompleted);
                }
                tasks.Add(store.CommitTransactionAsync(esTrans.TransactionId, stream));
                Task.WaitAll(tasks.ToArray());
            }

            Log.Info("Wrote {0} events to [{1}] (in single transaction)", events, stream);
            written.Signal();
        }

        private void ReadStream(CountdownEvent read, string stream, int @from, int count)
        {
            Log.Info("Reading [{0}] from {1,-10} count {2,-10}", stream, @from, count);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests))
            {
                for (var i = @from; i < @from + count; i++)
                {
                    var slice = store.ReadEventStream(stream, i, 1);
                    if(slice == null || slice.Events == null || slice.Events.Count() != 1)
                        throw new Exception(string.Format("Tried to read 1 event at position {0} from stream {1} but failed", i, stream));
                }
            }

            Log.Info("Done reading [{0}] from {1,-10} count {2,-10}", stream, @from, count);
            read.Signal();
        }
    }
}
