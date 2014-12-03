using EventStore.Common.Options;
using EventStore.Rags;
using System;

namespace EventStore.Documentation
{
    public class DocumentationGenerationOptions
    {
        public string[] EventStoreBinaryPaths { get; set; }
        public string OutputPath { get; set; }
        public DocumentationGenerationOptions()
        {
            OutputPath = "documentation.md";
        }
    }
    class DocumentationGeneration
    {
        public static int Main(string[] args)
        {
            try
            {
                var options = CommandLine.Parse<DocumentationGenerationOptions>(args)
                                .ApplyTo<DocumentationGenerationOptions>();
                var generator = new DocumentGenerator();
                generator.Generate(options.EventStoreBinaryPaths, options.OutputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
            return 1;
        }
    }
}
