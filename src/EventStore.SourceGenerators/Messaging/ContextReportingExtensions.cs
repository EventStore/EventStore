using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EventStore.SourceGenerators.Messaging {
	static class ContextReportingExtensions {
		static readonly DiagnosticDescriptor _impartialMessage = new(
			id: "ESGEN001",
			title: "Messages must be partial classes",
			messageFormat: "Class \"{0}\" must be partial",
			category: "EventStore.Messaging",
			DiagnosticSeverity.Error,
			isEnabledByDefault: true);

		public static void ReportImpartialMessage(this GeneratorExecutionContext context, ClassDeclarationSyntax node) =>
			context.ReportDiagnostic(Diagnostic.Create(_impartialMessage, node.GetLocation(), node.Identifier));
	}
}
