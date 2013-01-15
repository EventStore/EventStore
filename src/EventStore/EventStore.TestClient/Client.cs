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
using System.Threading;
using EventStore.BufferManagement;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.TestClient.Commands;
using EventStore.TestClient.Commands.DvuBasic;
using EventStore.Transport.Tcp;
using EventStore.Transport.Tcp.Formatting;
using EventStore.Transport.Tcp.Framing;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;

namespace EventStore.TestClient
{
    public class Client
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Client>();

        public readonly bool InteractiveMode;

        public readonly ClientOptions Options;
        public readonly IPEndPoint TcpEndpoint;
        public readonly IPEndPoint HttpEndpoint;

        private readonly BufferManager _bufferManager = new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);
        private readonly TcpClientConnector _connector = new TcpClientConnector();

        private readonly CommandsProcessor _commands = new CommandsProcessor(Log);

        public Client(ClientOptions options)
        {
            Options = options;

            TcpEndpoint = new IPEndPoint(options.Ip, options.TcpPort);
            HttpEndpoint = new IPEndPoint(options.Ip, options.HttpPort);

            InteractiveMode = options.Command.IsEmpty();

            RegisterProcessors();
        }

        private void RegisterProcessors()
        {
            _commands.Register(new UsageProcessor(_commands), usageProcessor: true);
            _commands.Register(new ExitProcessor());
            
            _commands.Register(new PingProcessor());
            _commands.Register(new PingFloodProcessor());
            _commands.Register(new PingFloodWaitingProcessor());

            _commands.Register(new PingFloodHttpProcessor());

            _commands.Register(new CreateStreamProcessor());

            _commands.Register(new WriteProcessor());
            _commands.Register(new WriteJsonProcessor());
            _commands.Register(new WriteFloodProcessor());
            _commands.Register(new WriteFloodWaitingProcessor());

            _commands.Register(new MultiWriteProcessor());
            _commands.Register(new MultiWriteFloodWaitingProcessor());

            _commands.Register(new TransactionWriteProcessor());

            _commands.Register(new WriteHttpProcessor());
            _commands.Register(new WriteFloodHttpProcessor());
            _commands.Register(new WriteFloodWaitingHttpProcessor());
            
            _commands.Register(new DeleteProcessor());

            _commands.Register(new ReadAllProcessor());
            _commands.Register(new ReadProcessor());
            _commands.Register(new ReadFloodProcessor());

            _commands.Register(new ReadHttpProcessor());
            _commands.Register(new ReadFloodHttpProcessor());

            _commands.Register(new WriteLongTermProcessor());
            _commands.Register(new WriteLongTermHttpProcessor());
            _commands.Register(new PingHttpLongTermProcessor());

            _commands.Register(new DvuBasicProcessor());
            _commands.Register(new RunTestScenariosProcessor());

            _commands.Register(new SubscribeToStreamProcessor());

            _commands.Register(new ScavengeProcessor());

            _commands.Register(new TcpSanitazationCheckProcessor());
        }

        public int Run()
        {
            if (!InteractiveMode)
                return Execute(Options.Command.ToArray());

            new Thread(() =>
            {
                Thread.Sleep(100);
                Console.Write(">>> ");

                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var args = ParseCommandLine(line);
                            Execute(args);
                        }
                        catch (Exception exc)
                        {
                            Log.ErrorException(exc, "Error during executing command.");
                        }
                    }
                    finally
                    {
                        Thread.Sleep(100);
                        Console.Write(">>> ");
                    }
                }
            }) { IsBackground = true, Name = "Client Main Loop Thread" }.Start();
            return 0;
        }

        private static string[] ParseCommandLine(string line)
        {
            return line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private int Execute(string[] args)
        {
            Log.Info("Processing command: {0}.", string.Join(" ", args));

            var context = new CommandProcessorContext(this, Log, new ManualResetEvent(true));

            int exitCode;
            if (_commands.TryProcess(context, args, out exitCode))
            {
                Log.Info("Command exited with code {0}.", exitCode);
                return exitCode;
            }

            return exitCode;
        }

        public Connection CreateTcpConnection(CommandProcessorContext context, 
                                              Action<Connection, TcpPackage> handlePackage,
                                              Action<Connection> connectionEstablished = null,
                                              Action<Connection, SocketError> connectionClosed = null,
                                              bool failContextOnError = true,
                                              IPEndPoint tcpEndPoint = null)
        {
            var connectionCreatedEvent = new AutoResetEvent(false);
            Connection typedConnection = null;

            var connection = _connector.ConnectTo(
                tcpEndPoint ?? TcpEndpoint,
                conn =>
                {
                    Log.Info("Connected to [{0}].", conn.EffectiveEndPoint);
                    if (connectionEstablished != null)
                    {
                        connectionCreatedEvent.WaitOne(500);
                        connectionEstablished(typedConnection);
                    }
                },
                (conn, error) =>
                {
                    var message = string.Format("Connection to [{0}] failed. Error: {1}.",
                                                conn.EffectiveEndPoint,
                                                error);
                    Log.Error(message);

                    if (connectionClosed != null)
                        connectionClosed(null, error);

                    if (failContextOnError)
                        context.Fail(reason: string.Format("Socket connection failed with error {0}.", error));
                });

            typedConnection = new Connection(connection, new RawMessageFormatter(_bufferManager), new LengthPrefixMessageFramer());
            typedConnection.ConnectionClosed +=
                (conn, error) =>
                {
                    Log.Info("Connection [{0}] was closed {1}",
                                conn.EffectiveEndPoint,
                                error == SocketError.Success ? "cleanly." : "with error: " + error + ".");

                    if (connectionClosed != null)
                        connectionClosed(conn, error);
                    else
                        Log.Info("connectionClosed callback was null");
                };
            connectionCreatedEvent.Set();

            typedConnection.ReceiveAsync(
                (conn, pkg) =>
                {
                    var package = new TcpPackage();
                    bool validPackage = false;
                    try
                    {
                        package = TcpPackage.FromArraySegment(new ArraySegment<byte>(pkg));
                        validPackage = true;

                        if (package.Command == TcpCommand.HeartbeatRequestCommand)
                        {
                            var resp = new TcpPackage(TcpCommand.HeartbeatResponseCommand, Guid.NewGuid(), null);
                            conn.EnqueueSend(resp.AsByteArray());
                            return;
                        }

                        handlePackage(conn, package);
                    }
                    catch (Exception ex)
                    {
                        Log.InfoException(ex,
                                          "[{0}] ERROR for {1}. Connection will be closed.",
                                          conn.EffectiveEndPoint,
                                          validPackage ? package.Command as object : "<invalid package>");
                        conn.Close();

                        if (failContextOnError)
                            context.Fail(ex);
                    }
                });

            return typedConnection;
        }
    }
}