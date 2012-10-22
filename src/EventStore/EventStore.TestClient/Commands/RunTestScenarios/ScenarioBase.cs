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
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using ILogger = EventStore.Common.Log.ILogger;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal abstract class ScenarioBase : IScenario
    {
        protected static readonly ILogger Log = LogManager.GetLoggerFor<ScenarioBase>();
        protected static readonly ClientAPI.ILogger ApiLogger = new ClientApiLogger();

        private string _dbPath;

        protected void CreateNewDbPath()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), "ES_" + Guid.NewGuid());
        }

        protected readonly IPEndPoint _tcpEndPoint;

        protected readonly Action<byte[]> DirectSendOverTcp;
        protected readonly int MaxConcurrentRequests;
        protected readonly int Connections;
        protected readonly int Streams;
        protected readonly int EventsPerStream;
        protected readonly int StreamDeleteStep;

        private readonly HashSet<int> _startedNodesProcIds;

        protected virtual TimeSpan StartupWaitInterval
        {
            get { return TimeSpan.FromSeconds(7); }
        }

        private readonly Dictionary<WriteMode, Func<string, int, Func<int, IEvent>, Task>> _writeHandlers;

        private readonly EventStoreConnection[] _connections;
        private int _nextConnectionNum = -1;

        protected ScenarioBase(Action<byte[]> directSendOverTcp,
                               int maxConcurrentRequests,
                               int connections,
                               int streams,
                               int eventsPerStream,
                               int streamDeleteStep)
        {
            DirectSendOverTcp = directSendOverTcp;
            MaxConcurrentRequests = maxConcurrentRequests;
            Connections = connections;
            Streams = streams;
            EventsPerStream = eventsPerStream;
            StreamDeleteStep = streamDeleteStep;

            _startedNodesProcIds = new HashSet<int>();
            CreateNewDbPath();
            _tcpEndPoint = new IPEndPoint(GetInterIpAddress(), 1113);

            _connections = new EventStoreConnection[connections];

            _writeHandlers = new Dictionary<WriteMode, Func<string, int, Func<int, IEvent>, Task>>
            {
                    {WriteMode.SingleEventAtTime, WriteSingleEventAtTime},
                    {WriteMode.Bucket, WriteBucketOfEventsAtTime},
                    {WriteMode.Transactional, WriteEventsInTransactionalWay}
            };
        }

        protected EventStoreConnection GetConnection()
        {
            var connectionNum = (int)(((uint)Interlocked.Increment(ref _nextConnectionNum)) % Connections);
            return _connections[connectionNum];
        }

        public void Run()
        {
            for (int i = 0; i < Connections; ++i)
            {
                _connections[i] = new EventStoreConnection(_tcpEndPoint, MaxConcurrentRequests, logger: ApiLogger);
            }
            RunInternal();   
        }

        protected abstract void RunInternal();

        public void Clean()
        {
            Log.Info("Deleting {0}...", _dbPath);
            Directory.Delete(_dbPath, true);
            Log.Info("Deleted {0}", _dbPath);
        }

        protected T[][] Split<T>(IEnumerable<T> sequence, int parts)
        {
            return sequence.Select((x, i) => new { GroupNum = i % parts, Item = x })
                           .GroupBy(x => x.GroupNum, y => y.Item)
                           .Select(x => x.ToArray())
                           .ToArray();
        }

        
        protected Task Write(WriteMode mode, string[] streams, int eventsPerStream)
        {
            Func<int, IEvent> createEvent = v => new TestEvent(v);
            return Write(mode, streams, eventsPerStream, createEvent);
        }

        protected Task Write(WriteMode mode, string[] streams, int eventsPerStream, Func<int, IEvent> createEvent)
        {
            Log.Info("Writing. Mode : {0,-15} Streams : {1,-10} Events per stream : {2,-10}",
                     mode,
                     streams.Length,
                     eventsPerStream);

            Func<string, int, Func<int, IEvent>, Task> handler;
            if (!_writeHandlers.TryGetValue(mode, out handler))
                throw new ArgumentOutOfRangeException("mode");

            var tasks = new List<Task>();
            for (var i = 0; i < streams.Length; i++)
            {
                //Console.WriteLine("WRITING TO {0}", streams[i]);
                tasks.Add(handler(streams[i], eventsPerStream, createEvent));
            }

            return Task.Factory.ContinueWhenAll(tasks.ToArray(), tsks =>
            {
                Task.WaitAll(tsks);
                Log.Info("Finished writing. Mode : {0,-15} Streams : {1,-10} Events per stream : {2,-10}",
                         mode,
                         streams.Length,
                         eventsPerStream);
            });
        }

        protected void DeleteStreams(IEnumerable<string> streams)
        {
            Log.Info("Deleting streams...");
            var store = GetConnection();

            var tasks = new List<Task>();
            foreach (var stream in streams)
            {
                var s = stream;
                Log.Info("Deleting stream {0}...", stream);
                var task = store.DeleteStreamAsync(stream, EventsPerStream)
                                .ContinueWith(x => Log.Info("Stream {0} successfully deleted", s));
                tasks.Add(task);
            }
            Task.WaitAll(tasks.ToArray());

            Log.Info("All streams successfully deleted");
        }

        protected Task CheckStreamsDeleted(IEnumerable<string> streams)
        {
            Log.Info("Verifying streams are deleted...");

            var store = GetConnection();
            var tasks = new List<Task>();

            foreach (var stream in streams)
            {
                var s = stream;
                var task = store.ReadEventStreamForwardAsync(stream, 0, 1).ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                        throw new Exception(string.Format("Stream '{0}' is not deleted, but should be!", s));
                });

                tasks.Add(task);
            }

            return Task.Factory.ContinueWhenAll(tasks.ToArray(), tsks =>
            {
                Task.WaitAll(tsks);
                Log.Info("Stream deletion verification succeeded.");
            });
        }

        protected Task Read(string[] streams, int @from, int count)
        {
            if (streams.Length == 0)
            {
                Debugger.Break();
                throw new Exception("Streams shouldn't be empty.");
            }
            Log.Info("Reading [{0}]\nfrom {1,-10} count {2,-10}", string.Join(",", streams), @from, count);

            var tasks = new List<Task>();

            for (int i = 0; i < streams.Length; i++)
            {
                var task = ReadStream(streams[i], from, count);
                tasks.Add(task);
            }

            return Task.Factory.ContinueWhenAll(tasks.ToArray(), tsks =>
            {
                Task.WaitAll(tsks);
                Log.Info("Done reading [{0}]", string.Join(",", streams));
            });
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
            if (nodeProcess == null || nodeProcess.HasExited)
                throw new ApplicationException("Process was not started.");

            Thread.Sleep(3000);
            Process tmp;
            var running = TryGetProcessById(nodeProcess.Id, out tmp);
            if (!running || tmp.HasExited)
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
            for (int i = 0; i < _connections.Length; ++i)
            {
                _connections[i].Close();
            }

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

        private Task WriteSingleEventAtTime(string stream, int events, Func<int, IEvent> createEvent)
        {
            var resSource = new TaskCompletionSource<object>();

            Log.Info("Starting to write {0} events to [{1}]", events, stream);
            var store = GetConnection();
            int eventVersion = 0;
            var createTask = store.CreateStreamAsync(stream, Encoding.UTF8.GetBytes("metadata"));

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteSingleEventAtTime for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            Action<Task> writeSingleEvent = null;
            writeSingleEvent = prevTask =>
            {
                if (eventVersion == events)
                {
                    Log.Info("Wrote {0} events to [{1}]", events, stream);
                    resSource.SetResult(null);
                    return;
                }

                eventVersion += 1;
                var writeTask = store.AppendToStreamAsync(stream,
                                                            eventVersion - 1,
                                                            new[] { createEvent(eventVersion) });
                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeSingleEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            createTask.ContinueWith(writeSingleEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            createTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);

            return resSource.Task;
        }

        private Task WriteBucketOfEventsAtTime(string stream, int eventCount, Func<int, IEvent> createEvent)
        {
            const int bucketSize = 25;
            Log.Info("Starting to write {0} events to [{1}] ({2} events at once)", eventCount, stream, bucketSize);

            var resSource = new TaskCompletionSource<object>();
            var store = GetConnection();
            int writtenCount = 0;
            var createTask = store.CreateStreamAsync(stream, Encoding.UTF8.GetBytes("metadata"));

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteBucketOfEventsAtTime for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            Action<Task> writeBatch = null;
            writeBatch = prevTask =>
            {
                if (writtenCount == eventCount)
                {
                    Log.Info("Wrote {0} events to [{1}] ({2} events at once)", eventCount, stream, bucketSize);
                    resSource.SetResult(null);
                    return;
                }

                var startIndex = writtenCount + 1;
                var endIndex = Math.Min(eventCount, startIndex + bucketSize - 1);
                var events = Enumerable.Range(startIndex, endIndex - startIndex + 1).Select(createEvent).ToArray();

                writtenCount = endIndex;

                var writeTask = store.AppendToStreamAsync(stream, startIndex - 1, events);
                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeBatch, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            createTask.ContinueWith(writeBatch, TaskContinuationOptions.OnlyOnRanToCompletion);
            createTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);

            return resSource.Task;
        }

        private Task WriteEventsInTransactionalWay(string stream, int eventCount, Func<int, IEvent> createEvent)
        {
            Log.Info("Starting to write {0} events to [{1}] (in single transaction)", eventCount, stream);

            var resSource = new TaskCompletionSource<object>();
            var store = GetConnection();

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteEventsInTransactionalWay for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            int writtenCount = 0;
            long transactionId = -1;
            var createTask = store.CreateStreamAsync(stream, Encoding.UTF8.GetBytes("metadata"));
            createTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);

            Action<Task> writeTransactionEvent = null;
            writeTransactionEvent = prevTask =>
            {
                if (writtenCount == eventCount)
                {
                    var commitTask = store.CommitTransactionAsync(transactionId, stream);
                    commitTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                    commitTask.ContinueWith(t =>
                    {
                        Log.Info("Wrote {0} events to [{1}] (in single transaction)", eventCount, stream);
                        resSource.SetResult(null);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    return;
                }

                writtenCount += 1;

                var writeTask = store.TransactionalWriteAsync(transactionId, stream, new[] { createEvent(writtenCount) });
                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeTransactionEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            createTask.ContinueWith(_ =>
            {
                var startTask = store.StartTransactionAsync(stream, 0);
                startTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                startTask.ContinueWith(t =>
                {
                    transactionId = t.Result.TransactionId;
                    writeTransactionEvent(t);
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

            }, TaskContinuationOptions.OnlyOnRanToCompletion);


            return resSource.Task;
        }

        private Task ReadStream(string stream, int from, int count)
        {
            Log.Info("Reading [{0}] from {1,-10} count {2,-10}", stream, from, count);
            var resSource = new TaskCompletionSource<object>();
            var store = GetConnection();

            Action<Task> fail = prevTask =>
            {
                Log.Info("ReadStream for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            var readTask = store.ReadEventStreamForwardAsync(stream, from, count);
            readTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
            readTask.ContinueWith(t =>
            {
                try
                {
                    var slice = t.Result;
                    if (slice == null || slice.Events == null || slice.Events.Length != count)
                    {
                        throw new Exception(string.Format(
                                "Tried to read {0} events from event number {1} from stream '{2}' but failed. Reason: {3}.",
                                count,
                                from,
                                stream,
                                slice == null ? "slice == null"
                                    : slice.Events == null ? "slive.Events == null"
                                    : slice.Events.Length != count ? string.Format("Expected count: {0}, actual count: {1}.", count, slice.Events.Length)
                                    : "WAT?!?"));
                    }

                    for (int i = 0; i < count; ++i)
                    {
                        var evnt = slice.Events[i];
                        if (evnt.EventNumber != i + from)
                        {
                            throw new Exception(string.Format(
                                "Received event with wrong event number. Expected: {0}, actual: {1}.\nEvent: {2}.",
                                from + i,
                                evnt.EventNumber,
                                evnt));
                        }
                    }
                    Log.Info("Done reading [{0}] from {1,-10} count {2,-10}", stream, from, count);
                    resSource.SetResult(null);
                }
                catch (Exception exc)
                {
                    Log.Info("ReadStream for stream {0} failed.", stream);
                    resSource.SetException(exc);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return resSource.Task;
        }
    }
}