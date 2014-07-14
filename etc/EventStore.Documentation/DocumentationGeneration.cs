using EventStore.Common.Options;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EventStore.Documentation
{
    public class DocumentationGenerationOptions
    {
        [ArgShortcut("b")]
        public string[] EventStoreBinaryPaths { get; set; }
        [ArgShortcut("o")]
        [DefaultValue("documentation.md")]
        public string OutputPath { get; set; }
    }
    class DocumentationGeneration
    {
        public static int Main(string[] args)
        {
            try
            {
                var options = Args.Parse<DocumentationGenerationOptions>(args);
                var generator = new DocumentGenerator();
                generator.Generate(options.EventStoreBinaryPaths, options.OutputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.Write(ex.Message);
            }
            return 1;
        }
    }
}
