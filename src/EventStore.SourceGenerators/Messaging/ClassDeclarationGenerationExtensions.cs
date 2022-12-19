using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EventStore.SourceGenerators.Messaging {
	static class ClassDeclarationGenerationExtensions {
		enum Kind {
			None,
			Base,
			Derived,
		};

		public static bool TryGetBaseMessageAttribute(this ClassDeclarationSyntax node, out AttributeSyntax attributeSyntax) =>
			node.TryGetAttribute("BaseMessage", minArgCount: 0, maxArgCount: 0, attributeSyntax: out attributeSyntax);

		public static bool TryGetDerivedMessageAttribute(this ClassDeclarationSyntax node, out AttributeSyntax attributeSyntax) =>
			node.TryGetAttribute("DerivedMessage", minArgCount: 0, maxArgCount: 0, attributeSyntax: out attributeSyntax);

		public static ClassDeclarationSyntax AddGeneratedMembers(
			this ClassDeclarationSyntax node,
			ClassDeclarationSyntax originalNode) =>

			GetStatsAttribute(originalNode) switch {
				(Kind.None, _) => node,
				(Kind.Base, _) => node.AddBaseStatsMembers(),
				(Kind.Derived, _) => node.AddDerivedStatsMembers(),
				_ => throw new InvalidOperationException(),
			};

		static (Kind, AttributeSyntax) GetStatsAttribute(
			ClassDeclarationSyntax node) {

			if (node.TryGetBaseMessageAttribute(out var attributeSyntax)) {
				return (Kind.Base, attributeSyntax);
			}

			if (node.TryGetDerivedMessageAttribute(out attributeSyntax)) {
				return (Kind.Derived, attributeSyntax);
			}

			return (Kind.None, default);
		}

		public static ClassDeclarationSyntax AddBaseStatsMembers(this ClassDeclarationSyntax node) =>
			node.AddMembers(DeclareDynamicMessageId())
				.AddMembers(RegisterDynamicMessageId(isRoot: true));

		public static ClassDeclarationSyntax AddDerivedStatsMembers(this ClassDeclarationSyntax node) =>
			node.AddMembers(RegisterDynamicMessageId(isRoot: false));

		private static string[] DeclareDynamicMessageId() => new[] {
			"private static int _nextMsgId = -1;",
			"protected static ref int NextMsgId => ref _nextMsgId;"
		};

		private static string[] RegisterDynamicMessageId(bool isRoot) {
			var virtualOrOverride = isRoot ? "virtual" : "override";
			return new[] {
				$"private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);",
				$"public {virtualOrOverride} int MsgTypeId => TypeId;",
			};
		}

		public static ClassDeclarationSyntax AddMembers(this ClassDeclarationSyntax node, params string[] members) {
			var parsed = members
				.Select(static x => SyntaxFactory.ParseMemberDeclaration(x))
				.ToArray();

			for (var i = 0; i < parsed.Length; i++) {
				if (parsed[i] is null)
					throw new Exception($"Could not parse member: {members[i]}");
			}

			return node.AddMembers(parsed);
		}
	}
}
