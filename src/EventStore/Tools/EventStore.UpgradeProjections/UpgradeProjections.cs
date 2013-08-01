// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
//
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
//
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
using System.Linq;
using System.Net;
using NDesk.Options;

namespace EventStore.UpgradeProjections
{
    class UpgradeProjections
    {
        private static string _ipString = "127.0.0.1";
        private static int _port = 1113;
        private static string _userName = "admin";
        private static string _password = "changeit";
        private static readonly OptionSet _parser;
        private static bool _helpInvoked;
        private static bool _runUpgrade;


        static UpgradeProjections()
        {
            _parser = new OptionSet
            {
                {"i|ip=", "the {IP} address of the upgraded EventStore instance", v => _ipString = v},
                {"p|port=", "the {PORT} to connect to", (int v) => _port = v},
                {"U|user=", "the {USER} account name with $admin privilages", v => _userName = v},
                {"P|password=", "the {PASSWORD} to use", v => _password = v},
                {"upgrade", "upgrade projectons", v => _runUpgrade = true},
                {"h|help", "show this help", v => Usage()}
            };
        }

        public static int Main(string[] args)
        {
            var unrecognized = _parser.Parse(args);
            if (_helpInvoked) 
                return 0;
            if (unrecognized.Count > 0)
            {
                Console.Error.WriteLine("Unrecognized command line: {0}", unrecognized.First());
                Usage();
                return 1;
            }
            try
            {
                var upgrade = new UpgradeProjectionsWorker(
                    IPAddress.Parse(_ipString), _port, _userName, _password, _runUpgrade);
                upgrade.RunUpgrade();
                return 0;
            }
            catch (AggregateException ex)
            {
                Console.Error.WriteLine(ex.Message);
                foreach (var inner in ex.InnerExceptions)
                {
                    Console.Error.WriteLine(inner.Message);
                }
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void Usage()
        {
            Console.WriteLine("Run this tool to upgrade projection definitions from the EventStore v1.x to v2.0");
            Console.WriteLine();
            _parser.WriteOptionDescriptions(Console.Out);
            _helpInvoked = true;
        }
    }
}
