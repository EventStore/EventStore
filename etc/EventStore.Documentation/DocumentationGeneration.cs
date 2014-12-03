using EventStore.Common.Options;
using EventStore.Rags;
using System;

namespace EventStore.Documentation
{
    public class DocumentationGenerationOptions
    {
        [ArgDescription("Path to the EventStore Binaries (e.g. c:\\EventStore\\)")]
        public string[] EventStoreBinaryPaths { get; set; }
        [ArgDescription("Output location of the generated Markdown File (e.g. c:\\generated_docs\\documentation.md)")]
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
                Console.Write(ArgUsage.GetUsage<DocumentationGenerationOptions>());
                Console.Error.WriteLine(ex.Message);
            }
            return 1;
        }
    }
}
