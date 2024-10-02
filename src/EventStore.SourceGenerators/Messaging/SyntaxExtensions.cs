using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EventStore.SourceGenerators.Messaging {
	static class SyntaxExtensions {
		public static bool TryGetAttribute(
			this MemberDeclarationSyntax node,
			string attributeName,
			int minArgCount,
			int maxArgCount,
			out AttributeSyntax attributeSyntax) {

			foreach (var attributeList in node.AttributeLists) {
				foreach (var candidate in attributeList.Attributes) {
					var actualArgCount = candidate.ArgumentList?.Arguments.Count ?? 0;
					if (candidate.Name.ToString() == attributeName &&
						actualArgCount >= minArgCount &&
						actualArgCount <= maxArgCount) {

						attributeSyntax = candidate;
						return true;
					}
				}
			}

			attributeSyntax = default;
			return false;
		}
	}
}
