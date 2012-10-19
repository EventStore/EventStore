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
using ILogger = EventStore.Common.Log.ILogger;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal abstract class ScenarioBase : IScenario
    {
        protected static readonly ILogger Log = LogManager.GetLoggerFor<Scenario1>();
        protected static readonly ClientAPI.ILogger ApiLogger = new ClientApiLogger();

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
                case WriteMode.SingleEventAtTimeSync:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteSingleEventAtTimeSync(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.Bucket:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteBucketOfEventsAtTime(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.BucketSync:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteBucketOfEventsAtTimeSync(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.Transactional:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteEventsInTransactionalWay(written, streams[i1], eventsPerStream));
                    }
                    break;
                case WriteMode.TransactionalSync:
                    for (var i = 0; i < streams.Length; i++)
                    {
                        int i1 = i;
                        ThreadPool.QueueUserWorkItem(_ => WriteEventsInTransactionalWaySync(written, streams[i1], eventsPerStream));
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
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
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

        private bool TryGetPathToMono(out string pathToMono)
        {
            const string monopathVariable = "EVENTSTORE_MONOPATH";
            pathToMono = Environment.GetEnvironmentVariable(monopathVariable);
            return !string.IsNullOrEmpty(pathToMono);
        }

        protected int StartNode()
        {
            var clientFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string fileName;
            string argumentsHead;

            string pathToMono;
            if (TryGetPathToMono(out pathToMono))
            {
                Log.Info("Mono at {0} will be used.", pathToMono);
                fileName = pathToMono;
                argumentsHead = string.Format("{0} {1}", "--gc=sgen", Path.Combine(clientFolder, "EventStore.SingleNode.exe"));
            }
            else
            {
                fileName = Path.Combine(clientFolder, "EventStore.SingleNode.exe");
                argumentsHead = "";
            }

            var arguments = string.Format("{0} --ip {1} -t {2} -h {3} --db {4}",
                                          argumentsHead,
                                          _tcpEndPoint.Address, 
                                          _tcpEndPoint.Port, 
                                          _tcpEndPoint.Port + 1000, 
                                          _dbPath);

            Log.Info("Starting [{0} {1}]...", fileName, arguments);

            var startInfo = new ProcessStartInfo(fileName, arguments);

            if (Common.Utils.OS.IsLinux)
            {
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
            }

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
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                var tasks = new List<Task>();
                for (var i = 0; i < events; i++)
                {
                    var task = store.AppendToStreamAsync(stream, i, new[] {new TestEvent(i + 1)});

                    tasks.Add(task);
                    if (i % 100 == 0)
                        tasks.RemoveAll(t => t.IsCompleted);
                }

                Task.WaitAll(tasks.ToArray());
            }
            Log.Info("Wrote {0} events to [{1}]", events, stream);
            written.Signal();
        }

        private void WriteSingleEventAtTimeSync(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}]", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                for (var i = 0; i < events; i++)
                    store.AppendToStream(stream, i, new[] {new TestEvent(i + 1)});
            }
            Log.Info("Wrote {0} events to [{1}]", events, stream);
            written.Signal();
        }

        private void WriteBucketOfEventsAtTime(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (100 events at once)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                var tasks = new List<Task>();

                var position = 0;
                var bucketSize = 100;

                while (position < events)
                {
                    var bucket = Enumerable.Range(position, bucketSize).Select(x => new TestEvent(x + 1));
                    var task = store.AppendToStreamAsync(stream, position, bucket);
                    tasks.Add(task);
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

        private void WriteBucketOfEventsAtTimeSync(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (100 events at once)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));

                var position = 0;
                var bucketSize = 100;

                while (position < events)
                {
                    var bucket = Enumerable.Range(position, bucketSize).Select(x => new TestEvent(x + 1));
                    store.AppendToStream(stream, position, bucket);
                    position += bucketSize;
                }

                if (position != events)
                {
                    var start = position - bucketSize;
                    var count = events - start;

                    var bucket = Enumerable.Range(start, count).Select(x => new TestEvent(x + 1));
                    store.AppendToStream(stream, start, bucket);
                }
            }

            Log.Info("Wrote {0} events to [{1}] (100 events at once)", events, stream);
            written.Signal();
        }

        private void WriteEventsInTransactionalWay(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (in single transaction)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));
                var esTrans = store.StartTransaction(stream, 0);

                var tasks = new List<Task>();
                for (var i = 0; i < events; i++)
                {
                    var task = store.TransactionalWriteAsync(esTrans.TransactionId, stream, new[] {new TestEvent(i + 1)});
                    tasks.Add(task);
                    if (i % 100 == 0)
                        tasks.RemoveAll(t => t.IsCompleted);
                }
                var commitTask = store.CommitTransactionAsync(esTrans.TransactionId, stream);
                tasks.Add(commitTask);
                Task.WaitAll(tasks.ToArray());
            }

            Log.Info("Wrote {0} events to [{1}] (in single transaction)", events, stream);
            written.Signal();
        }

        private void WriteEventsInTransactionalWaySync(CountdownEvent written, string stream, int events)
        {
            Log.Info("Starting to write {0} events to [{1}] (in single transaction)", events, stream);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
            {
                store.CreateStream(stream, Encoding.UTF8.GetBytes("metadata"));
                var esTrans = store.StartTransaction(stream, 0);

                for (var i = 0; i < events; i++)
                    store.TransactionalWrite(esTrans.TransactionId, stream, new[] {new TestEvent(i + 1)});

                store.CommitTransaction(esTrans.TransactionId, stream);
            }

            Log.Info("Wrote {0} events to [{1}] (in single transaction)", events, stream);
            written.Signal();
        }

        private void ReadStream(CountdownEvent read, string stream, int @from, int count)
        {
            Log.Info("Reading [{0}] from {1,-10} count {2,-10}", stream, @from, count);
            using (var store = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger))
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