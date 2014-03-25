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

	        Helper = new OptsHelper(() => FakeConfigsProperty, EnvPrefix, "config.json");
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