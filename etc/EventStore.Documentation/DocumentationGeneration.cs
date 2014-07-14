using EventStore.Common.Options;
using NDesk.Options;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EventStore.Documentation
{
    class DocumentationGeneration
    {
        private static List<string> _eventStoreBinaryPaths = new List<string>();
        private static string _outputPath = "documentation.md";
        private static bool _helpInvoked = false;
        private static readonly OptionSet _parser;
        static DocumentationGeneration()
        {
            _parser = new OptionSet
            {
                {"b|binaryPath=", "the {EventStoreBinaryPaths} that contains the Event Store Binaries", v => _eventStoreBinaryPaths.Add(v) },
                {"o|outputPath=", "the {OutputPath} for the generated documentation", v => _outputPath = v},
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
                var generator = new DocumentGenerator();
                generator.Generate(_eventStoreBinaryPaths.ToArray(), _outputPath);
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
            Console.WriteLine("Run this tool to generate the documentation for the options");
            Console.WriteLine();
            _parser.WriteOptionDescriptions(Console.Out);
            _helpInvoked = true;
        }
    }
}
