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
using PowerArgs;

namespace EventStore.UpgradeProjections
{
    public class UpgradeProjectionOptions
    {
        [DefaultValue("127.0.0.1")]
        public string Ip { get; set; }
        [DefaultValue(1113)]
        public int Port { get; set; }
        [DefaultValue("admin")]
        public string UserName { get; set; }
        [DefaultValue("changeit")]
        public string Password { get; set; }
        [DefaultValue(true)]
        public bool Upgrade { get; set; }
        public bool Help { get; set; }
    }
    class UpgradeProjections
    {
        public static int Main(string[] args)
        {
            var options = Args.Parse<UpgradeProjectionOptions>(args);
            if (options.Help)
            {
                Console.Write(ArgUsage.GetUsage<UpgradeProjectionOptions>());
                return 0;
            }
            try
            {
                var upgrade = new UpgradeProjectionsWorker(
                    IPAddress.Parse(options.Ip), options.Port, options.UserName, options.Password, options.Upgrade);
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
    }
}
