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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using EventStore.ClientAPI.Exceptions;
using EventStore.Common.Log;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.TestClient.Commands.RunTestScenarios;
using EventStore.Transport.Tcp;
using ILogger = EventStore.Common.Log.ILogger;

namespace EventStore.TestClient.Commands
{
    internal class RunTestScenariosProcessor : ICmdProcessor
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<RunTestScenariosProcessor>();
        private const string AllScenariosFlag = "ALL";

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
                const string usage = "<max concurrent requests, int> " +
                                     "\n<connections, int> " +
                                     "\n<streams count, int> " +
                                     "\n<eventsPerStream, int> " +
                                     "\n<streams delete step, int> " +
                                     "\n<scenario name, string, " + AllScenariosFlag + " for all scenarios>" +
                                     "\n<execution period minutes, int>" +
                                     "\n<dbParentPath or custom node, string or ip:tcp:http>";

                return usage;
            }
        }

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            if (args.Length != 0 && false == (args.Length == 7 || args.Length == 8))
                return false;

            var maxConcurrentRequests = 20;
            var connections = 10;
            var streams = 100;
            var eventsPerStream = 400;
            var streamDeleteStep = 7;
            var scenarioName = "";
            var executionPeriodMinutes = 2;
            string dbParentPath = null;
            NodeConnectionInfo customNode = null;

            {
                if (args.Length == 9)
                {
                    throw new ArgumentException("Not compatible arguments, only one of <dbPath> or <custom node> can be specified.");
                }

                if (args.Length == 8)
                {
                    IPAddress ip;
                    int tcpPort;
                    int httpPort;

                    var atoms = args[7].Split(':');
                    if (atoms.Length == 3
                        && IPAddress.TryParse(atoms[0], out ip)
                        && int.TryParse(atoms[1], out tcpPort)
                        && int.TryParse(atoms[2], out httpPort))
                    {
                        customNode = new NodeConnectionInfo(ip, tcpPort, httpPort);

                        args = CutLastArgument(args);
                    }
                }

                if (args.Length == 7 || args.Length == 8)
                {
                    try
                    {
                        maxConcurrentRequests = int.Parse(args[0]);
                        connections = int.Parse(args[1]);
                        streams = int.Parse(args[2]);
                        eventsPerStream = int.Parse(args[3]);
                        streamDeleteStep = int.Parse(args[4]);
                        scenarioName = args[5];
                        executionPeriodMinutes = int.Parse(args[6]);

                        if (args.Length == 8)
                        {
                            dbParentPath = args[7];
                        }
                        else
                        {
                            var envDbPath = Environment.GetEnvironmentVariable("EVENTSTORE_DATABASEPATH");
                            if (!string.IsNullOrEmpty(envDbPath))
                                dbParentPath = envDbPath;
                        }

                    }
                    catch (Exception e)
                    {
                        Log.Error("Invalid arguments ({0})", e.Message);
                        return false;
                    }
                }
            }

            context.IsAsync();

            Log.Info("\n---" +
                     "\nRunning scenario {0} using {1} connections with {2} max concurrent requests," +
                     "\nfor {3} streams {4} events each deleting every {5}th stream. " +
                     "\nExecution period {6} minutes. " +
                     "\nDatabase path {7};" +
                     "\nCustom Node {8};" +
                     "\n---",
                     scenarioName,
                     connections,
                     maxConcurrentRequests,
                     streams,
                     eventsPerStream,
                     streamDeleteStep,
                     executionPeriodMinutes,
                     dbParentPath,
                     customNode);

            var directTcpSender = CreateDirectTcpSender(context);
            var allScenarios = new IScenario[]
            {
                new LoopingScenario(directTcpSender, 
                                    maxConcurrentRequests, 
                                    connections, 
                                    streams, 
                                    eventsPerStream, 
                                    streamDeleteStep, 
                                    TimeSpan.FromMinutes(executionPeriodMinutes),
                                    dbParentPath,
                                    customNode), 
                new ProjectionsKillScenario(directTcpSender,
                                            maxConcurrentRequests,
                                            connections,
                                            streams,
                                            eventsPerStream,
                                            streamDeleteStep, 
                                            dbParentPath,
                                            customNode),
                new ProjForeachForcedCommonNameScenario(directTcpSender,
                                            maxConcurrentRequests,
                                            connections,
                                            streams,
                                            eventsPerStream,
                                            streamDeleteStep,
                                            TimeSpan.FromMinutes(executionPeriodMinutes),
                                            dbParentPath,
                                            customNode),
                new ProjForeachForcedCommonNameNoRestartScenario (directTcpSender, 
                                            maxConcurrentRequests, 
                                            connections, 
                                            streams, 
                                            eventsPerStream, 
                                            streamDeleteStep, 
                                            TimeSpan.FromMinutes(executionPeriodMinutes),
                                            dbParentPath,
                                            customNode), 
                new LoopingProjTranWriteScenario(directTcpSender, 
                                            maxConcurrentRequests, 
                                            connections, 
                                            streams, 
                                            eventsPerStream, 
                                            streamDeleteStep,
                                            TimeSpan.FromMinutes(executionPeriodMinutes),
                                            dbParentPath,
                                            customNode), 
                new LoopingProjectionKillScenario(directTcpSender, 
                                                  maxConcurrentRequests, 
                                                  connections, 
                                                  streams, 
                                                  eventsPerStream, 
                                                  streamDeleteStep, 
                                                  TimeSpan.FromMinutes(executionPeriodMinutes),
                                                  dbParentPath,
                                                  customNode), 
                new MassProjectionsScenario(directTcpSender, 
                                            maxConcurrentRequests, 
                                            connections, 
                                            streams, 
                                            eventsPerStream, 
                                            streamDeleteStep, 
                                            dbParentPath,
                                            customNode),
                new ProjectionWrongTagCheck(directTcpSender, 
                                            maxConcurrentRequests, 
                                            connections, 
                                            streams, 
                                            eventsPerStream, 
                                            streamDeleteStep, 
                                            TimeSpan.FromMinutes(executionPeriodMinutes),
                                            dbParentPath,
                                            customNode), 
                };

            Log.Info("Found scenarios {0} total :\n{1}.", allScenarios.Length, allScenarios.Aggregate(new StringBuilder(),
                                                                                                       (sb, s) => sb.AppendFormat("{0}, ", s.GetType().Name)));
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
                        Log.Info("Run scenario {0}", scenario.GetType().Name);
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

            if (context.ExitCode == 0)
                context.Success();

            return true;
        }

        private static string[] CutLastArgument(string[] args)
        {
            var cutArgs = new string[7];
            Array.Copy(args, cutArgs, 7);
            return cutArgs;
        }

        private Action<IPEndPoint, byte[]> CreateDirectTcpSender(CommandProcessorContext context)
        {
            const int timeoutMilliseconds = 4000;

            Action<IPEndPoint, byte[]> sender = (tcpEndPoint, bytes) =>
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

                context.Client.CreateTcpConnection(context, handlePackage, established, closed, false, tcpEndPoint);
                if (!sent.WaitOne(timeoutMilliseconds))
                    throw new ApplicationException("Connection to server was not closed in time.");
            };   

            return sender;
        }
    }
}
