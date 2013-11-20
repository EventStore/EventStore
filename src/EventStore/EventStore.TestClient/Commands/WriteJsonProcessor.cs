﻿// Copyright (c) 2012, Event Store LLP
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
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Http.Codecs;

namespace EventStore.TestClient.Commands
{
    internal class WriteJsonProcessor : ICmdProcessor
    {
        public string Usage { get { return "WRJ [<stream-id> <expected-version> <data> [<metadata>]]"; } }
        public string Keyword { get { return "WRJ"; } }

        private readonly Random _random = new Random();

        public bool Execute(CommandProcessorContext context, string[] args)
        {
            var eventStreamId = "test-stream";
            var expectedVersion = ExpectedVersion.Any;
            var data = GenerateTestData();
            string metadata = null;

            if (args.Length > 0)
            {
                if (args.Length < 3 || args.Length > 4)
                    return false;
                eventStreamId = args[0];
                expectedVersion = args[1].ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[1]);
                data = args[2];
                if (args.Length == 4)
                    metadata = args[3];
            }

            context.IsAsync();
            var writeDto = new TcpClientMessageDto.WriteEvents(
                eventStreamId,
                expectedVersion,
                new[]
                {
                    new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
                                                     "JsonDataEvent",
                                                     1,0,
                                                     Helper.UTF8NoBom.GetBytes(data),
                                                     Helper.UTF8NoBom.GetBytes(metadata ?? string.Empty))
                },
                false);
            var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), writeDto.Serialize());

            var sw = new Stopwatch();
            bool dataReceived = false;

            context.Client.CreateTcpConnection(
                context,
                connectionEstablished: conn =>
                {
                    context.Log.Info("[{0}, L{1}]: Writing...", conn.RemoteEndPoint, conn.LocalEndPoint);
                    sw.Start();
                    conn.EnqueueSend(package.AsByteArray());
                },
                handlePackage: (conn, pkg) =>
                {
                    if (pkg.Command != TcpCommand.WriteEventsCompleted)
                    {
                        context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
                        return;
                    }

                    dataReceived = true;
                    sw.Stop();

                    var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
                    if (dto.Result == TcpClientMessageDto.OperationResult.Success)
                    {
                        context.Log.Info("Successfully written. EventId: {0}.", package.CorrelationId);
                        PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword), (int)Math.Round(sw.Elapsed.TotalMilliseconds));
                    }
                    else
                    {
                        context.Log.Info("Error while writing: {0} ({1}).", dto.Message, dto.Result);
                    }

                    context.Log.Info("Write request took: {0}.", sw.Elapsed);
                    conn.Close();
                    context.Success();
                },
                connectionClosed: (connection, error) =>
                {
                    if (dataReceived && error == SocketError.Success)
                        context.Success();
                    else
                        context.Fail();
                });

            context.WaitForCompletion();
            return true;
        }

        private string GenerateTestData()
        {
            return Codec.Json.To(new TestData(Guid.NewGuid().ToString(), _random.Next(1, 101)));
        }
    }

    internal class TestData
    {
        public string Name { get; set; }
        public int Version { get; set; }

        public TestData()
        {
        }

        public TestData(string name, int version)
        {
            Name = name;
            Version = version;
        }
    }
}