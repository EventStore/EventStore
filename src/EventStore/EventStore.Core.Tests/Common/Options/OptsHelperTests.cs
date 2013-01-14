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
using System.IO;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using Mono.Options;
using NUnit.Framework;
using Newtonsoft.Json;

namespace EventStore.Core.Tests.Common.Options
{
    public abstract class OptsHelperTestBase : SpecificationWithDirectory
    {
        private const string EnvPrefix = "OPTSHELPER_";

        public string[] FakeConfigsProperty { get; private set; }

        protected OptsHelper Helper;

        private readonly List<Tuple<string, string>> _setVariables = new List<Tuple<string, string>>();

        public override void SetUp()
        {
            base.SetUp();

            Helper = new OptsHelper(() => FakeConfigsProperty, EnvPrefix);
            Helper.RegisterArray(() => FakeConfigsProperty, "cfg=", null, null, null, new string[0], "Configs.");
        }

        public override void TearDown()
        {
            UnsetEnvironment();
            base.TearDown();
        }

        private void UnsetEnvironment()
        {
            for (int i = _setVariables.Count - 1; i >= 0; --i)
            {
                Environment.SetEnvironmentVariable(_setVariables[i].Item1, _setVariables[i].Item2);
            }
            _setVariables.Clear();
        }

        protected void SetEnv(string envVariable, string value)
        {
            var envVar = (EnvPrefix + envVariable).ToUpper();
            _setVariables.Add(Tuple.Create(envVar, Environment.GetEnvironmentVariable(envVar)));
            Environment.SetEnvironmentVariable(envVar, value);
        }

        protected string WriteJsonConfig(object cfg)
        {
            Ensure.NotNull(cfg, "cfg");

            var s = JsonConvert.SerializeObject(cfg, Formatting.Indented);
            var file = GetTempFilePath();

            Console.WriteLine("Writing to file {0}:", file);
            Console.WriteLine(s);
            Console.WriteLine();

            File.WriteAllText(file, s);

            return file;
        }
    }

    [TestFixture]
    public class OptsHelperTests : OptsHelperTestBase
    {
        [Test]
        public void test1()
        {

            bool showHelp = false;
            var optionSet = new OptionSet()
                    .Add("h|help",
                         "Show help.",
                         v =>
                         {
                             Console.WriteLine(v);
                             showHelp = true;
                         })
                    .Add("i=|ip=",
                         "The IP address to bind to.",
                         (IPAddress v) =>
                         {
                             Console.WriteLine(v);
                         })
                    .Add("cfg=",
                         "Comma-separated list of configuration files.",
                         config =>
                         {
                             Console.WriteLine("Config: {0}", config);
                         });
            try
            {
                optionSet.Parse(new[] {"--help", "--ip=127.0.0.1", "--cfg", "a", "--cfg=b", "--cfg:c"});
                if (showHelp)
                    ShowHelp("Usage:", optionSet);
            }
            catch (OptionException exc)
            {
                ShowHelp(string.Format("Error: {0}\n\nUsage:", exc.Message), optionSet);
            }

//            new OptionSet()
//            .Add("i|ip", , }
//        [Option("i", "ip", Required = true, HelpText="")]
//        public IPAddress Ip { get; set; }
//
//        [Option("t", "tcp-port", Required = true, HelpText="The port to run the TCP server on.")]
//        public int TcpPort { get; set; }
//
//        [Option("h", "http-port", Required = true, HelpText="The port to run the HTTP server on.")]
//        public int HttpPort { get; set; }
//
//        [Option("s", "stats-period-sec", DefaultValue = 30, HelpText="The number of seconds between statistics gathers.")]
//        public int StatsPeriodSec { get; set; }
//
//        [Option("c", "chunkcache", DefaultValue = 2, HelpText = "The number of chunks to cache in unmanaged memory.")]
//        public int ChunksToCache { get; set; }
//
//        [Option(null, "db", HelpText = "The path the db should be loaded/saved to.")]
//        public string DbPath { get; set; }
//
//        [Option(null, "do-not-verify-db-hashes-on-startup", DefaultValue = false, HelpText = "Bypasses the checking of file hashes of database during startup (allows for faster startup).")]
//        public bool DoNotVerifyDbHashesOnStartup { get; set; }
//
//        [Option(null, "run-projections", DefaultValue = false, HelpText = "Enables the running of Javascript projections (experimental).")]
//        public bool RunProjections { get; set; }
//
//        [Option(null, "projection-threads", DefaultValue = 3, HelpText = "The number of threads to use for projections.")]
//        public int ProjectionThreads { get; set; }
//
//        [Option(null, "tcp-send-threads", DefaultValue = 3, HelpText = "The number of threads to use for sending to TCP sockets.")]
//        public int TcpSendThreads { get; set; }
//
//        [Option(null, "http-receive-threads", DefaultValue = 5, HelpText = "The number of threads to use for receiving from HTTP.")]
//        public int HttpReceiveThreads { get; set; }
//
//        [Option(null, "http-send-threads", DefaultValue = 3, HelpText = "The number of threads for sending over HTTP.")]
//        public int HttpSendThreads { get; set; }
//
//        [Option(null, "prefixes", HelpText = "The prefixes that the http server should respond to.")]
//        public string PrefixesString { get; set; }  
        }

        private static void ShowHelp(string message, OptionSet optionSet)
        {
            Console.WriteLine(message);
            optionSet.WriteOptionDescriptions(Console.Out);
        }
    }

}
