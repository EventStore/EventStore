using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using EventStore.Common.Options;
using Mono.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{

    public class OptsHelper
    {
        public void Register(Expression<Func<bool>> member, string cmdPrototype, string jsonPath, string envName, 
                             string description, bool? @default = null)
        {
        }

        public void Register<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                                string description, T? @default = null) 
            where T : struct
        {
        }

        public void Register<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                                string description, T @default = null) 
            where T : class
        {
        }

        public void Register<T>(Expression<Func<T[]>> member, string cmdPrototype, string jsonPath, 
                                string description, T[] @default = null)
        {
        }

        public T Get<T>(Expression<Func<T>> member)
        {
            throw new NotImplementedException();
        }
    }


    public class SingleNodeOptions
    {
        private readonly OptsHelper _helper;

        public IPAddress Ip { get { return _helper.Get(() => Ip); } }
        public int TcpPort { get { return _helper.Get(() => TcpPort); } }
        public int HttpPort { get { return _helper.Get(() => HttpPort); } }
        public int StatsPeriodSec { get { return _helper.Get(() => StatsPeriodSec); } }
        public int ChunksToCache { get { return _helper.Get(() => ChunksToCache); } }
        public string DbPath { get { return _helper.Get(() => DbPath); } }
        public bool DoNotVerifyDbHashesOnStartup { get { return _helper.Get(() => DoNotVerifyDbHashesOnStartup); } }
        public bool RunProjections { get { return _helper.Get(() => RunProjections); } }
        public int ProjectionThreads { get { return _helper.Get(() => ProjectionThreads); } }
        public int TcpSendThreads { get { return _helper.Get(() => TcpSendThreads); } }
        public int HttpReceiveThreads { get { return _helper.Get(() => HttpReceiveThreads); } }
        public int HttpSendThreads { get { return _helper.Get(() => HttpSendThreads); } }
        public string[] HttpPrefixes { get { return _helper.Get(() => HttpPrefixes); } }

        public string[] Configs { get { return _helper.Get(() => Configs); } }

        public SingleNodeOptions()
        {
            _helper = new OptsHelper(/*() => Configs, "EVENTSTORE_"*/);
            _helper.Register(() => Ip, "i|ip=", "net.ip", "ip", "The IP address to bind to.", IPAddress.Loopback);
            _helper.Register(() => TcpPort, "t|tcp-port=", null, null, "The port to run the TCP server on.", (int?)1113);
            _helper.Register(() => HttpPort, "h|http-port=", null, null, "The port to run the HTTP server on.", (int?)2113);
            _helper.Register(() => StatsPeriodSec, "s|stats-period-sec=", null, null, "The number of seconds between statistics gathers.", (int?)30);
            _helper.Register(() => ChunksToCache, "c|chunkcache=", null, null, "The number of chunks to cache in unmanaged memory.", (int?)2);
            _helper.Register(() => DbPath, "db=", null, null, "The path the db should be loaded/saved to.", string.Empty);
            _helper.Register(() => DoNotVerifyDbHashesOnStartup, "skip-db-verify|do-not-verify-db-hashes-on-startup", null, null, "Bypasses the checking of file hashes of database during startup (allows for faster startup).", (bool?)false);
            _helper.Register(() => RunProjections, "run-projections", null, null, "Enables the running of JavaScript projections (experimental).", (bool?)false);
            _helper.Register(() => ProjectionThreads, "projection-threads=", null, null, "The number of threads to use for projections.", (int?)3);
            _helper.Register(() => TcpSendThreads, "tcp-send-threads=", null, null, "The number of threads to use for sending to TCP sockets.", (int?)3);
            _helper.Register(() => HttpReceiveThreads, "http-receive-threads=", null, null, "The number of threads to use for receiving from HTTP.", (int?)5);
            _helper.Register(() => HttpSendThreads, "http-send-threads=", null, null, "The number of threads for sending over HTTP.", (int?)3);
            _helper.Register(() => HttpPrefixes, "prefixes|http-prefix=", null, null, "The prefixes that the http server should respond to.", new string[0]);

            //_helper.Parse();
        }
    }



    [TestFixture]
    public class OptsHelperTests
    {
        [Test]
        public void test1()
        {
            TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressTypeConverter)));

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
