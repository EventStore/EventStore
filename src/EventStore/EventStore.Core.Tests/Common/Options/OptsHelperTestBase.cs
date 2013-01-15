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
using EventStore.Common.Options;
using EventStore.Common.Utils;
using Newtonsoft.Json;

namespace EventStore.Core.Tests.Common.Options
{
    public abstract class OptsHelperTestBase : SpecificationWithDirectory
    {
        private const string EnvPrefix = "OPTSHELPER_";

        public string[] FakeConfigsProperty { get { throw new InvalidOperationException(); } }

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

//            Console.WriteLine("Writing to file {0}:", file);
//            Console.WriteLine(s);
//            Console.WriteLine();

            File.WriteAllText(file, s);

            return file;
        }
    }
}