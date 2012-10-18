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
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Common.Log;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands
{
    internal class RunTestScenariosProcessor : ICmdProcessor
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<RunTestScenariosProcessor>();

        public string Keyword
        {
            get
            {
                return string.Format("RT");
            }
        }

        private const string AllScenariosFlag = "ALL";

        public string Usage
        {
            get
            {
                return string.Format("{0} " +
                                     "<max concurrent requests, default = 100> " +
                                     "<threads, default = 20> " +
                                     "<streams, default = 20> " +
                                     "<eventsPerStream, default = 10000> " +
                                     "<streams delete step, default = 7> " +
                                     "<scenario name, default = LoopingScenario, " + AllScenariosFlag + " for all scenarios>" +
                                     "<execution period minutes, default = 10>",
                                     Keyword);
            }
        }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            if (args.Length != 0 && args.Length != 7)
                return false;

            var maxConcurrentRequests = 100;
            var threads = 20;
            var streams = 20;
            var eventsPerStream = 10000;
            var streamDeleteStep = 7;
            var scenarioName = "LoopingScenario";
            var executionPeriodMinutes = 10;

            if (args.Length == 7)
            {
                try
                {
                    maxConcurrentRequests = int.Parse(args[0]);
                    threads = int.Parse(args[1]);
                    streams = int.Parse(args[2]);
                    eventsPerStream = int.Parse(args[3]);
                    streamDeleteStep = int.Parse(args[4]);
                    scenarioName = args[5];
                    executionPeriodMinutes = int.Parse(args[6]);
                }
                catch (Exception e)
                {
                    Log.Error("Invalid arguments ({0})", e.Message);
                    return false;
                }
            }

            context.IsAsync();

            Log.Info("Running scenario {0} using {1} threads, {2} streams {3} events each deleting every {4}th stream. " +
                     "Period {5} minutes. " +
                     "Max concurrent ES requests {6}",
                     scenarioName,
                     threads,
                     streams,
                     eventsPerStream,
                     streamDeleteStep,
                     executionPeriodMinutes,
                     maxConcurrentRequests);

            var directTcpSender = CreateDirectTcpSender(context);
            var allScenarios = new IScenario[]
                {
                    new Scenario1(directTcpSender, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep),
                    //new Scenario2(directTcpSender, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep) 
                    new LoopingScenario(directTcpSender, 
                                        maxConcurrentRequests, 
                                        threads, 
                                        streams, 
                                        eventsPerStream, 
                                        streamDeleteStep, 
                                        TimeSpan.FromMinutes(executionPeriodMinutes))
                };

            Log.Info("Found scenarios ({0} total).", allScenarios.Length);
            var scenarios = allScenarios.Where(x => scenarioName == AllScenariosFlag 
                                                    || x.GetType().Name.Equals(scenarioName, StringComparison.InvariantCultureIgnoreCase))
                                        .ToArray();

            Log.Info("Running test scenarios ({0} total)...", scenarios.Length);

            foreach (var scenario in scenarios)
            {
                using (scenario)
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
            }
            Log.Info("Finished running test scenarios");

            context.Success();
            return true;
        }

        private Action<byte[]> CreateDirectTcpSender(CommandProcessorContext context)
        {
            Action<byte[]> sender = bytes =>
            {
                var sent = new AutoResetEvent(false);

                Action<TcpTypedConnection<byte[]>, TcpPackage> handlePackage = (_, __) => { };
                Action<TcpTypedConnection<byte[]>> established = connection =>
                {
                    connection.EnqueueSend(bytes);
                    connection.Close();
                    sent.Set();
                };
                Action<TcpTypedConnection<byte[]>, SocketError> closed = (_, __) => sent.Set();

                context.Client.CreateTcpConnection(context, handlePackage, established, closed, false);
                sent.WaitOne();
            };   

            return sender;
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

    internal interface IScenario : IDisposable
    {
        void Run();
        void Clean();
    }

    internal class Scenario1 : ScenarioBase
    {
        public Scenario1(Action<byte[]> directSendOverTcp, int maxConcurrentRequests, int threads, int streams, int eventsPerStream, int streamDeleteStep)
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
        }

        public override void Run()
        {
            ThreadPool.SetMaxThreads(Threads, Threads);

            var nodeProcessId = StartNode();

            var streams = Enumerable.Range(0, Streams).Select(i => string.Format("test-stream-{0}", i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            Read(exceptDeleted, from: 0, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

            KillNode(nodeProcessId);
        }
    }

    internal class LoopingScenario : ScenarioBase
    {
        private readonly TimeSpan _executionPeriod;
        
        private readonly Random _rnd = new Random();
        private volatile bool _stopParalleWrites;
        private TimeSpan _startupWaitInterval;

        protected override TimeSpan StartupWaitInterval
        {
            get { return _startupWaitInterval; }
        }

        public LoopingScenario(Action<byte[]> directSendOverTcp, 
                               int maxConcurrentRequests, 
                               int threads, 
                               int streams, 
                               int eventsPerStream, 
                               int streamDeleteStep,
                               TimeSpan executionPeriod) 
            : base(directSendOverTcp, maxConcurrentRequests, threads, streams, eventsPerStream, streamDeleteStep)
        {
            _executionPeriod = executionPeriod;
            SetStartupWaitInterval(TimeSpan.FromSeconds(7));
        }

        private void SetStartupWaitInterval(TimeSpan interval)
        {
            _startupWaitInterval = interval;
        }

        public override void Run()
        {
            var stopWatch = Stopwatch.StartNew();

            var runIndex = 0;
            while (stopWatch.Elapsed < _executionPeriod)
            {
                Log.Info("=================== Start run #{0}, elapsed {1} of {2} minutes =================== ",
                    runIndex,
                    (int)stopWatch.Elapsed.TotalMinutes,
                    _executionPeriod.TotalMinutes);

                SetStartupWaitInterval(TimeSpan.FromSeconds(7 + (2 * runIndex) % 200));
                InnerRun(runIndex);
                runIndex += 1;
            }
        }

        public void InnerRun(int runIndex)
        {
            const int internalThreadsCount = 2;
            ThreadPool.SetMaxThreads(Threads + internalThreadsCount, Threads + internalThreadsCount);

            var nodeProcessId = StartNode();
            if (runIndex % 2 == 0)
                Scavenge();

            var parallelWritesEvent = RunParallelWrites(runIndex);

            var streams = Enumerable.Range(0, Streams).Select(i => FormatStreamName(runIndex, i)).ToArray();
            var slices = Split(streams, 3);

            Write(WriteMode.SingleEventAtTime, slices[0], EventsPerStream);
            Write(WriteMode.Bucket, slices[1], EventsPerStream);
            Write(WriteMode.Transactional, slices[2], EventsPerStream);

            var deleted = streams.Where((s, i) => i % StreamDeleteStep == 0).ToArray();
            DeleteStreams(deleted);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
            nodeProcessId = StartNode();

            parallelWritesEvent = RunParallelWrites(runIndex);

            CheckStreamsDeleted(deleted);

            var exceptDeleted = streams.Except(deleted).ToArray();
            Read(exceptDeleted, from: 0, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream - Piece, count: Piece + 1);
            Read(exceptDeleted, from: EventsPerStream / 2, count: Math.Min(Piece + 1, EventsPerStream - EventsPerStream / 2));

            Log.Info("== READ from picked ALL ==");

            var pickedFromAllStreamsForRead = Enumerable.Range(0, runIndex)
                                              .SelectMany(run => Enumerable.Range(0, Streams)
                                              .Where(s => s % StreamDeleteStep != 0)
                                              .Select(s => FormatStreamName(run, s)))
                                              .Where(x => _rnd.Next(100) > (10 + 1000 / runIndex)).ToArray();

            var pickedFromAllStreamsDeleted = Enumerable.Range(0, runIndex)
                                              .SelectMany(run => Enumerable.Range(0, Streams)
                                              .Where(s => s % StreamDeleteStep == 0)
                                              .Select(s => FormatStreamName(run, s)))
                                              .Where(x => _rnd.Next(100) > (5 + 1000 / runIndex)).ToArray();

            Read(pickedFromAllStreamsForRead, 0, EventsPerStream / 5);
            Read(pickedFromAllStreamsForRead, EventsPerStream / 2, EventsPerStream / 5);

            CheckStreamsDeleted(pickedFromAllStreamsDeleted);

            _stopParalleWrites = true;
            if (!parallelWritesEvent.WaitOne(60000))
                throw new ApplicationException("Parallel writes stop timed out.");

            KillNode(nodeProcessId);
        }

        private AutoResetEvent RunParallelWrites(int runIndex)
        {
            _stopParalleWrites = false;
            var resetEvent = new AutoResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
                {
                    while (!_stopParalleWrites)
                    {
                        var parallelStreams = Enumerable.Range(0, 2)
                        .Select(x => string.Format("parallel-write-stream-in{0}-{1}-{2}",
                            runIndex,
                            x,
                            string.Format("rnd{0}-{1}", _rnd.Next(), DateTime.UtcNow.Ticks)))
                        .ToArray();

                        Thread.Sleep(1);
                        
                        const int eventsPerStream = 1000;

                        Write(WriteMode.SingleEventAtTime, parallelStreams, eventsPerStream);
                        Read(parallelStreams, 0, eventsPerStream / 6);
                        Read(parallelStreams, eventsPerStream / 3, eventsPerStream / 6);
                        Read(parallelStreams, eventsPerStream - eventsPerStream / 10, eventsPerStream / 10);
                    }
                    resetEvent.Set();
                });
            return resetEvent;
        }

        private static string FormatStreamName(int runIndex, int i)
        {
            return string.Format("stream-in{0}-{1}", runIndex, i);
        }
    }

    internal abstract class ScenarioBase : IScenario
    {
        protected static readonly ILogger Log = LogManager.GetLoggerFor<Scenario1>();

        private string _dbPath;

        protected void CreateNewDbPath()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "ES_" + Guid.NewGuid());
        }

        private readonly IPEndPoint _tcpEndPoint;

        protected readonly Action<byte[]> DirectSendOverTcp;
        protected readonly int MaxConcurrentRequests;
        protected readonly int Threads;
        protected readonly int Streams;
        protected readonly int EventsPerStream;
        protected readonly int StreamDeleteStep;
        protected readonly int Piece;

        private readonly HashSet<int> _startedNodesProcIds; 

        protected virtual TimeSpan StartupWaitInterval
        {
            get { return TimeSpan.FromSeconds(7); }
        }

        protected ScenarioBase(Action<byte[]> directSendOverTcp,
                               int maxConcurrentRequests, 
                               int threads, 
                               int streams, 
                               int eventsPerStream, 
                               int streamDeleteStep)
        {
            DirectSendOverTcp = directSendOverTcp;
            MaxConcurrentRequests = maxConcurrentRequests;
            Threads = threads;
            Streams = streams;
            EventsPerStream = eventsPerStream;
            StreamDeleteStep = streamDeleteStep;
            Piece = 100;

            _startedNodesProcIds = new HashSet<int>();

            CreateNewDbPath();

            var ip = GetInterIpAddress();
            _tcpEndPoint = new IPEndPoint(ip, 1113);
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
                        store.ReadEventStreamForward(stream, 0, 1);
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

        protected int StartNode()
        {
            var clientFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var fileName = Path.Combine(clientFolder, "EventStore.SingleNode.exe");
            var arguments = string.Format("--ip {0} -t {1} -h {2} --db {3}", _tcpEndPoint.Address, _tcpEndPoint.Port, _tcpEndPoint.Port + 1000, _dbPath);

            Log.Info("Starting [{0} {1}]...", fileName, arguments);

            var startInfo = new ProcessStartInfo(fileName, arguments)
                                                {
                                                    UseShellExecute = false, 
                                                    RedirectStandardOutput = true
                                                };

            var nodeProcess = Process.Start(startInfo);

            if (nodeProcess == null)
                throw new ApplicationException("Process was not started.");

            _startedNodesProcIds.Add(nodeProcess.Id);
            Log.Info("Started node with process id {0}", nodeProcess.Id);

            Thread.Sleep(StartupWaitInterval);
            Log.Info("Started [{0} {1}]", fileName, arguments);

            return nodeProcess.Id;
        }

        private IPAddress GetInterIpAddress()
        {
            var interIp = IPAddress.None;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    interIp = ip;
                    break;
                }
            }
            return interIp;
        }

        private bool TryGetProcessById(int processId, out Process process)
        {
            process = null;

            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            return true;
        }

        protected void KillNode(int processId)
        {
            Log.Info("Killing {0}...", processId);

            Process process;
            if (TryGetProcessById(processId, out process))
            {
                _startedNodesProcIds.Remove(processId);

                process.Kill();
                while (!process.HasExited)
                    Thread.Sleep(200);

                Thread.Sleep(2000);
                Log.Info("Killed process {0}", processId);
            }
            else
                Log.Error("Process with ID {0} was not found to be killed.", processId);
        }

        public void Dispose()
        {
            try
            {
                _startedNodesProcIds.ToList().ForEach(KillNode);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to kill started nodes: {0}.", ex.Message);
            }
        }

        protected void Scavenge()
        {
            Log.Info("Send scavenge command...");
            var package = new TcpPackage(TcpCommand.ScavengeDatabase, Guid.NewGuid(), null).AsByteArray();
            DirectSendOverTcp(package);
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
                    var slice = store.ReadEventStreamForward(stream, i, 1);
                    if(slice == null || slice.Events == null || slice.Events.Count() != 1)
                        throw new Exception(string.Format("Tried to read 1 event at position {0} from stream {1} but failed", i, stream));
                }
            }

            Log.Info("Done reading [{0}] from {1,-10} count {2,-10}", stream, @from, count);
            read.Signal();
        }
    }
}
