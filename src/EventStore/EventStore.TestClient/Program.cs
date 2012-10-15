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
using System.IO;
using System.Linq;
using System.Net;
using EventStore.Common.CommandLine.lib;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.TestClient
{
    public class Program
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<Program>();

        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var exc = eventArgs.ExceptionObject as Exception;
                if (exc != null)
                    Log.FatalException(exc, "Global Unhandled Exception occurred within {0}.", sender);
                else
                    Log.Fatal("Global Unhandled Exception ({0}) occurred within {1}.", eventArgs.ExceptionObject, sender);
            };

            try
            {
                Console.Title = string.Format("EventStore - CLIENT - {0}", DateTime.UtcNow);

                var options = new ClientOptions();
                if (!CommandLineParser.Default.ParseArguments(args, options))
                {
                    Console.WriteLine("Error parsing arguments. Exiting...");
                    return -1;
                }

                var logsDir = !string.IsNullOrEmpty(options.LogsDir) ? options.LogsDir : Helper.GetDefaultLogsDir();
                LogManager.Init("client", logsDir);

                IPAddress ipAddr;
                if (!IPAddress.TryParse(options.Ip, out ipAddr))
                {
                    Log.Error("Wrong IP address provided: {0}.", ipAddr);
                    return -1;
                }

                var systemInfo = String.Format("{0} {1}", OS.IsLinux ? "Linux" : "Windows", Runtime.IsMono ? "MONO" : ".NET");
                var startInfo = String.Join(Environment.NewLine, options.GetLoadedOptionsPairs().Select(pair => String.Format("{0} : {1}", pair.Key, pair.Value)));
                var logsDirectory = String.Format("LOGS DIRECTORY : {0}", LogManager.LogsDirectory);

                Log.Info(String.Format("{0}{1}{2}{1}{3}", logsDirectory, Environment.NewLine, systemInfo, startInfo));

                var client = new Client(options);
                var exitCode = client.Run();

                Log.Info("Exit code: {0}.", exitCode);
                return exitCode;
            }
            catch(Exception exc)
            {
                Log.ErrorException(exc, "Exception during execution of client.");
                return -1;
            }
            finally
            {
                // VERY IMPORTANT TO PREVENT DEADLOCKING ON MONO
                LogManager.Finish();
            }
        }
    }
}