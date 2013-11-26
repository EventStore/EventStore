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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Log;
using EventStore.Core.Services;
using EventStore.Core.Tests.Helpers;
using ConsoleLogger = EventStore.ClientAPI.Common.Log.ConsoleLogger;
using ILogger = EventStore.Common.Log.ILogger;
using TcpCommand = EventStore.Core.Services.Transport.Tcp.TcpCommand;
using TcpPackage = EventStore.Core.Services.Transport.Tcp.TcpPackage;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal abstract class ScenarioBase : IScenario
    {
        protected static readonly ILogger Log = LogManager.GetLoggerFor<ScenarioBase>();
        protected static readonly ClientAPI.ILogger ApiLogger = new ClientApiLoggerBridge(LogManager.GetLogger("client-api"));

        protected readonly UserCredentials AdminCredentials = new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword);
        protected readonly Action<IPEndPoint, byte[]> DirectSendOverTcp;
        protected readonly int MaxConcurrentRequests;
        protected readonly int Connections;
        protected readonly int Streams;
        protected readonly int EventsPerStream;
        protected readonly int StreamDeleteStep;

        private readonly NodeConnectionInfo _customNodeConnection;
        private readonly NodeConnectionInfo _nodeConnection;
        
        private readonly HashSet<int> _startedNodesProcIds;

        private readonly string _dbPath;

        private readonly Dictionary<WriteMode, Func<string, int, Func<int, EventData>, Task>> _writeHandlers;
        private readonly IEventStoreConnection[] _connections;
        private int _nextConnectionNum = -1;
        private readonly ProjectionsManager _projectionsManager;

        protected virtual TimeSpan StartupWaitInterval
        {
            get { return TimeSpan.FromSeconds(12); }
        }

        protected ScenarioBase(Action<IPEndPoint, byte[]> directSendOverTcp,
                               int maxConcurrentRequests,
                               int connections,
                               int streams,
                               int eventsPerStream,
                               int streamDeleteStep,
                               string dbParentPath,
                               NodeConnectionInfo customNodeConnection)
        {
            DirectSendOverTcp = directSendOverTcp;
            MaxConcurrentRequests = maxConcurrentRequests;
            Connections = connections;
            Streams = streams;
            EventsPerStream = eventsPerStream;
            StreamDeleteStep = streamDeleteStep;
            _customNodeConnection = customNodeConnection;

            _startedNodesProcIds = new HashSet<int>();

            var ipAddress = IPAddress.Loopback;

            if (_customNodeConnection != null)
            {
                _nodeConnection = _customNodeConnection;
                _dbPath = null;
            }
            else
            {
                _dbPath = CreateNewDbPath(dbParentPath);
                _nodeConnection = new NodeConnectionInfo(ipAddress,
                                                         PortsHelper.GetAvailablePort(ipAddress),
                                                         PortsHelper.GetAvailablePort(ipAddress));
            }

            _connections = new IEventStoreConnection[connections];

            Log.Info("Projection manager points to {0}.", _nodeConnection);
            _projectionsManager = new ProjectionsManager(new ConsoleLogger(), new IPEndPoint(_nodeConnection.IpAddress, _nodeConnection.HttpPort));

            _writeHandlers = new Dictionary<WriteMode, Func<string, int, Func<int, EventData>, Task>>
            {
                    {WriteMode.SingleEventAtTime, WriteSingleEventAtTime},
                    {WriteMode.Bucket, WriteBucketOfEventsAtTime},
                    {WriteMode.Transactional, WriteEventsInTransactionalWay}
            };
        }

        private static string CreateNewDbPath(string dbParentPath)
        {
            var dbParent = dbParentPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var dataFolder = Path.Combine(dbParent, "data");
            var idx = 0;
            var dbPath = Path.Combine(dataFolder, string.Format("es_{0}", idx));

            while (Directory.Exists(dbPath))
            {
                idx += 1;
                dbPath = Path.Combine(dataFolder, string.Format("es_{0}", idx));
            }
            return dbPath;
        }

        protected IEventStoreConnection GetConnection()
        {
            var connectionNum = (int)(((uint)Interlocked.Increment(ref _nextConnectionNum)) % Connections);
            return _connections[connectionNum];
        }

        protected ProjectionsManager GetProjectionsManager()
        {
            return _projectionsManager;
        }

        public void Run()
        {
            const int maxReconnections = 200;
            const int maxOperationRetries = 200;

            for (int i = 0; i < Connections; ++i)
            {
                _connections[i] = EventStoreConnection.Create(
                      ConnectionSettings.Create()
                                        .UseCustomLogger(ApiLogger)
                                        .LimitConcurrentOperationsTo(MaxConcurrentRequests)
                                        .LimitRetriesForOperationTo(maxReconnections)
                                        .LimitReconnectionsTo(maxOperationRetries)
                                        .FailOnNoServerResponse(),
                    new IPEndPoint(_nodeConnection.IpAddress, _nodeConnection.TcpPort),
                    string.Format("ESConn-{0}", i));
                _connections[i].Closed += (s, e) => Log.Debug("[SCENARIO] {0} closed.", e.Connection.ConnectionName);
                _connections[i].Connected += (s, e) => Log.Debug("[SCENARIO] {0} connected to [{1}].", e.Connection.ConnectionName, e.RemoteEndPoint);
                _connections[i].Disconnected += (s, e) => Log.Debug("[SCENARIO] {0} disconnected from [{1}].", e.Connection.ConnectionName, e.RemoteEndPoint);
                _connections[i].Reconnecting += (s, e) => Log.Debug("[SCENARIO] {0} reconnecting.", e.Connection.ConnectionName);
                _connections[i].ErrorOccurred += (s, e) => Log.DebugException(e.Exception, "[SCENARIO] {0} error occurred.", e.Connection.ConnectionName);
                _connections[i].Connect();
            } 
            RunInternal();   
        }

        protected abstract void RunInternal();

        public void Clean()
        {
            CloseConnections();
            KillStartedNodes();
            DeleteDatabase();
        }

        private void DeleteDatabase()
        {
            try
            {
                if (_dbPath != null)
                {
                    Log.Info("Deleting {0}...", _dbPath);
                    Directory.Delete(_dbPath, true);
                    Log.Info("Deleted {0}", _dbPath);
                }
            }
            catch (IOException ex)
            {
                Log.ErrorException(ex, "Failed to delete dir {0}, IOException was raised", _dbPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.ErrorException(ex, "Failed to delete dir {0}, UnauthorizedAccessException was raised", _dbPath);
            }
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
            Func<int, EventData> createEvent = TestEvent.NewTestEvent;
            return Write(mode, streams, eventsPerStream, createEvent);
        }

        protected Task Write(WriteMode mode, string[] streams, int eventsPerStream, Func<int, EventData> createEvent)
        {
            Log.Info("Writing. Mode : {0,-15} Streams : {1,-10} Events per stream : {2,-10}",
                     mode,
                     streams.Length,
                     eventsPerStream);

            Func<string, int, Func<int, EventData>, Task> handler;
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
                var task = store.DeleteStreamAsync(stream, (EventsPerStream - 1), hardDelete: true)
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
                var task = store.ReadStreamEventsForwardAsync(stream, 0, 1, resolveLinkTos: false).ContinueWith(t =>
                {
                    if (t.Result.Status != SliceReadStatus.StreamDeleted)
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
            int processId = -1;
            if (_customNodeConnection == null)
                processId = StartNewNode();

            return processId;
        }

        private int StartNewNode()
        {
            var clientFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string fileName;
            string argumentsHead;

            string pathToMono;
            if (TryGetPathToMono(out pathToMono))
            {
                Log.Info("Mono at {0} will be used.", pathToMono);
                fileName = pathToMono;
                argumentsHead = string.Format("--debug --gc=sgen {0}", Path.Combine(clientFolder, "EventStore.SingleNode.exe"));
            }
            else
            {
                fileName = Path.Combine(clientFolder, "EventStore.SingleNode.exe");
                argumentsHead = "";
            }

            var arguments = string.Format("{0} --run-projections --ip {1} -t {2} -h {3} --db {4}",
                                          argumentsHead,
                                          _nodeConnection.IpAddress,
                                          _nodeConnection.TcpPort,
                                          _nodeConnection.HttpPort,
                                          _dbPath);

            Log.Info("Starting [{0} {1}]...", fileName, arguments);

            var startInfo = new ProcessStartInfo(fileName, arguments);

            var nodeProcess = Process.Start(startInfo);
            if (nodeProcess == null || nodeProcess.HasExited)
                throw new ApplicationException(string.Format("Process was not started [{0} {1}].", fileName, arguments));

            Thread.Sleep(3000);
            Process tmp;
            var running = TryGetProcessById(nodeProcess.Id, out tmp);
            if (!running || tmp.HasExited)
                throw new ApplicationException(string.Format("Process was not started [{0} {1}].", fileName, arguments));

            _startedNodesProcIds.Add(nodeProcess.Id);

            Log.Info("Started node with process id {0}", nodeProcess.Id);

            Thread.Sleep(StartupWaitInterval);
            Log.Info("Started [{0} {1}]", fileName, arguments);

            return nodeProcess.Id;
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
            if (processId != -1)
                KillStartedNode(processId);
            else
                Log.Info("Skip killing, procId -1");
        }

        private void KillStartedNode(int processId)
        {
            Log.Info("Killing {0}...", processId);

            Process process;
            if (TryGetProcessById(processId, out process))
            {
                process.Kill();

                var waitCount = 200;
                while (!process.HasExited && waitCount > 0)
                {
                    Thread.Sleep(250);
                    waitCount -= 1;
                }

                if (process.HasExited)
                {
                    _startedNodesProcIds.Remove(processId);

                    PortsHelper.ReturnPort(_nodeConnection.TcpPort);
                    PortsHelper.ReturnPort(_nodeConnection.HttpPort);

                    Log.Info("Killed process {0}, wait a bit.", processId);
                    Thread.Sleep(1000); // wait for system to release port used by HttpListener.
                }
                else
                {
                    Process temp;
                    if (TryGetProcessById(processId, out temp))
                        Log.Error(
                            "Process {0} did not report about exit in time and is still present in processes list.",
                            processId);
                    else
                        Log.Info("Process {0} did not report about exit in time but is not found again.", processId);
                }
            }
            else
                Log.Error("Process {0} was not found to be killed.", processId);
        }

        public void Dispose()
        {
            CloseConnections();
            Thread.Sleep(2 * 1000);
            KillStartedNodes();
        }

        private void CloseConnections()
        {
            for (int i = 0; i < _connections.Length; ++i)
            {
                _connections[i].Close();
            }
        }

        private void KillStartedNodes()
        {
            Log.Info("Killing remaining nodes...");
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
            DirectSendOverTcp(new IPEndPoint(_nodeConnection.IpAddress, _nodeConnection.TcpPort), package);
            Log.Info("Scavenge command was sent.");
        }

        private Task WriteSingleEventAtTime(string stream, int events, Func<int, EventData> createEvent)
        {
            var resSource = new TaskCompletionSource<object>();

            Log.Info("Starting to write {0} events to [{1}]", events, stream);
            var store = GetConnection();
            int eventVersion = 0;

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteSingleEventAtTime for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            Action<Task> writeSingleEvent = null;
            writeSingleEvent = _ =>
            {
                if (eventVersion == events)
                {
                    Log.Info("Wrote {0} events to [{1}]", events, stream);
                    resSource.SetResult(null);
                    return;
                }

                var writeTask = store.AppendToStreamAsync(stream,
                                                          eventVersion - 1,
                                                          new[] { createEvent(eventVersion) });

                eventVersion += 1;

                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeSingleEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            writeSingleEvent(null);

            return resSource.Task;
        }

        private Task WriteBucketOfEventsAtTime(string stream, int eventCount, Func<int, EventData> createEvent)
        {
            const int bucketSize = 25;
            Log.Info("Starting to write {0} events to [{1}] ({2} events at once)", eventCount, stream, bucketSize);

            var resSource = new TaskCompletionSource<object>();
            var store = GetConnection();
            int writtenCount = 0;

            Action<Task> fail = prevTask =>
            {
                Log.Info("WriteBucketOfEventsAtTime for stream {0} failed.", stream);
                resSource.SetException(prevTask.Exception);
            };

            Action<Task> writeBatch = null;
            writeBatch = _ =>
            {
                if (writtenCount == eventCount)
                {
                    Log.Info("Wrote {0} events to [{1}] ({2} events at once)", eventCount, stream, bucketSize);
                    resSource.SetResult(null);
                    return;
                }

                var startIndex = writtenCount;
                var endIndex = Math.Min(eventCount, startIndex + bucketSize);
                var events = Enumerable.Range(startIndex, endIndex - startIndex).Select(createEvent).ToArray();

                writtenCount = endIndex;

                var expectedVersion = startIndex - 1;
                var writeTask = store.AppendToStreamAsync(stream, expectedVersion, events);

                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeBatch, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            writeBatch(null);

            return resSource.Task;
        }
        
        private Task WriteEventsInTransactionalWay(string stream, int eventCount, Func<int, EventData> createEvent)
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
            EventStoreTransaction transaction = null;

            Action<Task> writeTransactionEvent = null;
            writeTransactionEvent = prevTask =>
            {
                if (writtenCount == eventCount)
                {
                    var commitTask = transaction.CommitAsync();
                    commitTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                    commitTask.ContinueWith(t =>
                    {
                        Log.Info("Wrote {0} events to [{1}] (in single transaction)", eventCount, stream);
                        resSource.SetResult(null);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    return;
                }

                var writeTask = transaction.WriteAsync(createEvent(writtenCount));

                writtenCount += 1;

                writeTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
                writeTask.ContinueWith(writeTransactionEvent, TaskContinuationOptions.OnlyOnRanToCompletion);
            };

            var startTask = store.StartTransactionAsync(stream, -1);
            startTask.ContinueWith(fail, TaskContinuationOptions.OnlyOnFaulted);
            startTask.ContinueWith(t =>
            {
                transaction = t.Result;
                writeTransactionEvent(t);
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

            var readTask = store.ReadStreamEventsForwardAsync(stream, @from, count, resolveLinkTos: false);
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
                        var evnt = slice.Events[i].Event;
                        if (evnt.EventNumber != i + from)
                        {
                            throw new Exception(string.Format(
                                "Received event with wrong event number. Expected: {0}, actual: {1}.\nEvent: {2}.",
                                from + i,
                                evnt.EventNumber,
                                evnt));
                        }

                        TestEvent.VerifyIfMatched(evnt);
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