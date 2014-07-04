//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013 Antoine Aubry
    
//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:
    
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
    
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EventStore.Common.Yaml.Core.Events;
using ParsingEvent = EventStore.Common.Yaml.Core.Events.ParsingEvent;
using TagDirective = EventStore.Common.Yaml.Core.Tokens.TagDirective;
using VersionDirective = EventStore.Common.Yaml.Core.Tokens.VersionDirective;

namespace EventStore.Common.Yaml.Core
{
	/// <summary>
	/// Emits YAML streams.
	/// </summary>
	public class Emitter : IEmitter
	{
		private const int MinBestIndent = 2;
		private const int MaxBestIndent = 9;
		private const int MaxAliasLength = 128;

		private static readonly Regex uriReplacer = new Regex(@"[^0-9A-Za-z_\-;?@=$~\\\)\]/:&+,\.\*\(\[!]",
			RegexOptions.Compiled | RegexOptions.Singleline);

		private readonly TextWriter output;

		private readonly bool isCanonical;
		private readonly int bestIndent;
		private readonly int bestWidth;
		private EmitterState state;

		private readonly Stack<EmitterState> states = new Stack<EmitterState>();
		private readonly Queue<ParsingEvent> events = new Queue<ParsingEvent>();
		private readonly Stack<int> indents = new Stack<int>();
		private readonly TagDirectiveCollection tagDirectives = new TagDirectiveCollection();
		private int indent;
		private int flowLevel;
		private bool isMappingContext;
		private bool isSimpleKeyContext;
		private bool isRootContext;

		private int column;
		private bool isWhitespace;
		private bool isIndentation;

		private bool isOpenEnded;
		private bool isDocumentEndWritten;

		private readonly AnchorData anchorData = new AnchorData();
		private readonly TagData tagData = new TagData();
		private readonly ScalarData scalarData = new ScalarData();

		private class AnchorData
		{
			public string anchor;
			public bool isAlias;
		}

		private class TagData
		{
			public string handle;
			public string suffix;
		}

		private class ScalarData
		{
			public string value;
			public bool isMultiline;
			public bool isFlowPlainAllowed;
			public bool isBlockPlainAllowed;
			public bool isSingleQuotedAllowed;
			public bool isBlockAllowed;
			public ScalarStyle style;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Emitter"/> class.
		/// </summary>
		/// <param name="output">The <see cref="TextWriter"/> where the emitter will write.</param>
		public Emitter(TextWriter output)
			: this(output, MinBestIndent)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Emitter"/> class.
		/// </summary>
		/// <param name="output">The <see cref="TextWriter"/> where the emitter will write.</param>
		/// <param name="bestIndent">The preferred indentation.</param>
		public Emitter(TextWriter output, int bestIndent)
			: this(output, bestIndent, int.MaxValue)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Emitter"/> class.
		/// </summary>
		/// <param name="output">The <see cref="TextWriter"/> where the emitter will write.</param>
		/// <param name="bestIndent">The preferred indentation.</param>
		/// <param name="bestWidth">The preferred text width.</param>
		public Emitter(TextWriter output, int bestIndent, int bestWidth)
			: this(output, bestIndent, bestWidth, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Emitter"/> class.
		/// </summary>
		/// <param name="output">The <see cref="TextWriter"/> where the emitter will write.</param>
		/// <param name="bestIndent">The preferred indentation.</param>
		/// <param name="bestWidth">The preferred text width.</param>
		/// <param name="isCanonical">If true, write the output in canonical form.</param>
		public Emitter(TextWriter output, int bestIndent, int bestWidth, bool isCanonical)
		{
			if (bestIndent < MinBestIndent || bestIndent > MaxBestIndent)
			{
				throw new ArgumentOutOfRangeException("bestIndent", string.Format(CultureInfo.InvariantCulture,
					"The bestIndent parameter must be between {0} and {1}.", MinBestIndent, MaxBestIndent));
			}

			this.bestIndent = bestIndent;

			if (bestWidth <= bestIndent * 2)
			{
				throw new ArgumentOutOfRangeException("bestWidth", "The bestWidth parameter must be greater than bestIndent * 2.");
			}

			this.bestWidth = bestWidth;

			this.isCanonical = isCanonical;

			this.output = output;
		}

		/// <summary>
		/// Emit an evt.
		/// </summary>
		public void Emit(ParsingEvent @event)
		{
			events.Enqueue(@event);

			while (!NeedMoreEvents())
			{
				var current = events.Peek();
				AnalyzeEvent(current);
				StateMachine(current);

				// Only dequeue after calling state_machine because it checks how many events are in the queue.
				// Todo: well, move into StateMachine() then
				events.Dequeue();
			}
		}

		/// <summary>
		/// Check if we need to accumulate more events before emitting.
		/// 
		/// We accumulate extra
		///  - 1 event for DOCUMENT-START
		///  - 2 events for SEQUENCE-START
		///  - 3 events for MAPPING-START
		/// </summary>
		private bool NeedMoreEvents()
		{
			if (events.Count == 0)
			{
				return true;
			}

			int accumulate;
			switch (events.Peek().Type)
			{
				case EventType.DocumentStart:
					accumulate = 1;
					break;

				case EventType.SequenceStart:
					accumulate = 2;
					break;

				case EventType.MappingStart:
					accumulate = 3;
					break;

				default:
					return false;
			}

			if (events.Count > accumulate)
			{
				return false;
			}

			var level = 0;
			foreach (var evt in events)
			{
				switch (evt.Type)
				{
					case EventType.DocumentStart:
					case EventType.SequenceStart:
					case EventType.MappingStart:
						++level;
						break;

					case EventType.DocumentEnd:
					case EventType.SequenceEnd:
					case EventType.MappingEnd:
						--level;
						break;
				}
				if (level == 0)
				{
					return false;
				}
			}

			return true;
		}

		private void AnalyzeEvent(ParsingEvent evt)
		{
			anchorData.anchor = null;
			tagData.handle = null;
			tagData.suffix = null;

			var alias = evt as AnchorAlias;
			if (alias != null)
			{
				AnalyzeAnchor(alias.Value, true);
				return;
			}

			var nodeEvent = evt as NodeEvent;
			if (nodeEvent != null)
			{
				var scalar = evt as Scalar;
				if (scalar != null)
				{
					AnalyzeScalar(scalar.Value);
				}

				AnalyzeAnchor(nodeEvent.Anchor, false);

				if (!string.IsNullOrEmpty(nodeEvent.Tag) && (isCanonical || nodeEvent.IsCanonical))
				{
					AnalyzeTag(nodeEvent.Tag);
				}
			}
		}

		private void AnalyzeAnchor(string anchor, bool isAlias)
		{
			anchorData.anchor = anchor;
			anchorData.isAlias = isAlias;
		}

		private void AnalyzeScalar(string value)
		{
			scalarData.value = value;

			if (value.Length == 0)
			{
				scalarData.isMultiline = false;
				scalarData.isFlowPlainAllowed = false;
				scalarData.isBlockPlainAllowed = true;
				scalarData.isSingleQuotedAllowed = true;
				scalarData.isBlockAllowed = false;
				return;
			}

			var flowIndicators = false;
			var blockIndicators = false;
			if (value.StartsWith("---", StringComparison.Ordinal) || value.StartsWith("...", StringComparison.Ordinal))
			{
				flowIndicators = true;
				blockIndicators = true;
			}

			var buffer = new CharacterAnalyzer<StringLookAheadBuffer>(new StringLookAheadBuffer(value));
			var preceededByWhitespace = true;
			var followedByWhitespace = buffer.IsWhiteBreakOrZero(1);

			var leadingSpace = false;
			var leadingBreak = false;
			var trailingSpace = false;
			var trailingBreak = false;
			
			var breakSpace = false;
			var spaceBreak = false;
			var previousSpace = false;
			var previousBreak = false;

			var lineBreaks = false;
			var specialCharacters = false;

			var isFirst = true;
			while (!buffer.EndOfInput)
			{
				if (isFirst)
				{
					if (buffer.Check(@"#,[]{}&*!|>\""%@`"))
					{
						flowIndicators = true;
						blockIndicators = true;
					}

					if (buffer.Check("?:"))
					{
						flowIndicators = true;
						if (followedByWhitespace)
							blockIndicators = true;
					}

					if (buffer.Check('-') && followedByWhitespace)
					{
						flowIndicators = true;
						blockIndicators = true;
					}
				}
				else
				{
					if (buffer.Check(",?[]{}"))
						flowIndicators = true;

					if (buffer.Check(':'))
					{
						flowIndicators = true;
						if (followedByWhitespace)
							blockIndicators = true;
					}

					if (buffer.Check('#') && preceededByWhitespace)
					{
						flowIndicators = true;
						blockIndicators = true;
					}
				}

				if (!buffer.IsPrintable() || (!buffer.IsAscii() && !IsUnicode(output.Encoding)))
					specialCharacters = true;

				if (buffer.IsBreak())
					lineBreaks = true;

				if (buffer.IsSpace())
				{
					if (isFirst)
						leadingSpace = true;

					if (buffer.Buffer.Position >= buffer.Buffer.Length - 1)
						trailingSpace = true;

					if (previousBreak)
						breakSpace = true;

					previousSpace = true;
					previousBreak = false;
				}
				else if (buffer.IsBreak())
				{
					if (isFirst)
						leadingBreak = true;

					if (buffer.Buffer.Position >= buffer.Buffer.Length - 1)
						trailingBreak = true;

					if (previousSpace)
						spaceBreak = true;

					previousSpace = false;
					previousBreak = true;
				}
				else
				{
					previousSpace = false;
					previousBreak = false;
				}

				preceededByWhitespace = buffer.IsWhiteBreakOrZero();
				buffer.Skip(1);
				if (!buffer.EndOfInput)
					followedByWhitespace = buffer.IsWhiteBreakOrZero(1);

				isFirst = false;
			}

			scalarData.isFlowPlainAllowed = true;
			scalarData.isBlockPlainAllowed = true;
			scalarData.isSingleQuotedAllowed = true;
			scalarData.isBlockAllowed = true;

			if (leadingSpace || leadingBreak || trailingSpace || trailingBreak)
			{
				scalarData.isFlowPlainAllowed = false;
				scalarData.isBlockPlainAllowed = false;
			}

			if (trailingSpace)
				scalarData.isBlockAllowed = false;

			if (breakSpace)
			{
				scalarData.isFlowPlainAllowed = false;
				scalarData.isBlockPlainAllowed = false;
				scalarData.isSingleQuotedAllowed = false;
			}

			if (spaceBreak || specialCharacters)
			{
				scalarData.isFlowPlainAllowed = false;
				scalarData.isBlockPlainAllowed = false;
				scalarData.isSingleQuotedAllowed = false;
				scalarData.isBlockAllowed = false;
			}

			scalarData.isMultiline = lineBreaks;
			if (lineBreaks)
			{
				scalarData.isFlowPlainAllowed = false;
				scalarData.isBlockPlainAllowed = false;
			}

			if (flowIndicators)
				scalarData.isFlowPlainAllowed = false;

			if (blockIndicators)
				scalarData.isBlockPlainAllowed = false;
		}

		private bool IsUnicode(Encoding encoding)
		{
			return encoding.Equals(Encoding.UTF8) ||
				encoding.Equals(Encoding.Unicode) ||
				encoding.Equals(Encoding.BigEndianUnicode) ||
				encoding.Equals(Encoding.UTF7) ||
				encoding.Equals(Encoding.UTF32);
		}

		private void AnalyzeTag(string tag)
		{
			tagData.handle = tag;
			foreach (var tagDirective in tagDirectives)
			{
				if (tag.StartsWith(tagDirective.Prefix, StringComparison.Ordinal))
				{
					tagData.handle = tagDirective.Handle;
					tagData.suffix = tag.Substring(tagDirective.Prefix.Length);
					break;
				}
			}
		}

		private void StateMachine(ParsingEvent evt)
		{
			switch (state)
			{
				case EmitterState.StreamStart:
					EmitStreamStart(evt);
					break;

				case EmitterState.FirstDocumentStart:
					EmitDocumentStart(evt, true);
					break;

				case EmitterState.DocumentStart:
					EmitDocumentStart(evt, false);
					break;

				case EmitterState.DocumentContent:
					EmitDocumentContent(evt);
					break;

				case EmitterState.DocumentEnd:
					EmitDocumentEnd(evt);
					break;

				case EmitterState.FlowSequenceFirstItem:
					EmitFlowSequenceItem(evt, true);
					break;

				case EmitterState.FlowSequenceItem:
					EmitFlowSequenceItem(evt, false);
					break;

				case EmitterState.FlowMappingFirstKey:
					EmitFlowMappingKey(evt, true);
					break;

				case EmitterState.FlowMappingKey:
					EmitFlowMappingKey(evt, false);
					break;

				case EmitterState.FlowMappingSimpleValue:
					EmitFlowMappingValue(evt, true);
					break;

				case EmitterState.FlowMappingValue:
					EmitFlowMappingValue(evt, false);
					break;

				case EmitterState.BlockSequenceFirstItem:
					EmitBlockSequenceItem(evt, true);
					break;

				case EmitterState.BlockSequenceItem:
					EmitBlockSequenceItem(evt, false);
					break;

				case EmitterState.BlockMappingFirstKey:
					EmitBlockMappingKey(evt, true);
					break;

				case EmitterState.BlockMappingKey:
					EmitBlockMappingKey(evt, false);
					break;

				case EmitterState.BlockMappingSimpleValue:
					EmitBlockMappingValue(evt, true);
					break;

				case EmitterState.BlockMappingValue:
					EmitBlockMappingValue(evt, false);
					break;

				case EmitterState.StreamEnd:
					throw new YamlException("Expected nothing after STREAM-END");

				default:
					throw new InvalidOperationException();
			}
		}

		/// <summary>
		/// Expect STREAM-START.
		/// </summary>
		private void EmitStreamStart(ParsingEvent evt)
		{
			if (!(evt is StreamStart))
			{
				throw new ArgumentException("Expected STREAM-START.", "evt");
			}

			indent = -1;
			column = 0;
			isWhitespace = true;
			isIndentation = true;

			state = EmitterState.FirstDocumentStart;
		}

		/// <summary>
		/// Expect DOCUMENT-START or STREAM-END.
		/// </summary>
		private void EmitDocumentStart(ParsingEvent evt, bool isFirst)
		{
			var documentStart = evt as DocumentStart;
			if (documentStart != null)
			{
				var isImplicit = documentStart.IsImplicit && isFirst && !isCanonical;

				var documentTagDirectives = NonDefaultTagsAmong(documentStart.Tags);

				if (!isFirst && !isDocumentEndWritten && (documentStart.Version != null || documentTagDirectives.Count > 0))
				{
					isDocumentEndWritten = false;
					WriteIndicator("...", true, false, false);
					WriteIndent();
				}

				if (documentStart.Version != null)
				{
					AnalyzeVersionDirective(documentStart.Version);

					isImplicit = false;
					WriteIndicator("%YAML", true, false, false);
					WriteIndicator(string.Format(CultureInfo.InvariantCulture,
						"{0}.{1}", Constants.MajorVersion, Constants.MinorVersion),
						true, false, false);
					WriteIndent();
				}

				foreach (var tagDirective in documentTagDirectives)
				{
					AppendTagDirectiveTo(tagDirective, false, tagDirectives);
				}

				foreach (var tagDirective in Constants.DefaultTagDirectives)
				{
					AppendTagDirectiveTo(tagDirective, true, tagDirectives);
				}

				if (documentTagDirectives.Count > 0)
				{
					isImplicit = false;
					foreach (var tagDirective in Constants.DefaultTagDirectives)
					{
						AppendTagDirectiveTo(tagDirective, true, documentTagDirectives);
					}

					foreach (var tagDirective in documentTagDirectives)
					{
						WriteIndicator("%TAG", true, false, false);
						WriteTagHandle(tagDirective.Handle);
						WriteTagContent(tagDirective.Prefix, true);
						WriteIndent();
					}
				}

				if (CheckEmptyDocument())
				{
					isImplicit = false;
				}

				if (!isImplicit)
				{
					WriteIndent();
					WriteIndicator("---", true, false, false);
					if (isCanonical)
					{
						WriteIndent();
					}
				}

				state = EmitterState.DocumentContent;
			}

			else if (evt is StreamEnd)
			{
				if (isOpenEnded)
				{
					WriteIndicator("...", true, false, false);
					WriteIndent();
				}

				state = EmitterState.StreamEnd;
			}
			else
			{
				throw new YamlException("Expected DOCUMENT-START or STREAM-END");
			}
		}

		private TagDirectiveCollection NonDefaultTagsAmong(IEnumerable<TagDirective> tagCollection)
		{
			var directives = new TagDirectiveCollection();
			if (tagCollection == null)
				return directives;

			foreach (var tagDirective in tagCollection)
			{
				AppendTagDirectiveTo(tagDirective, false, directives);
			}
			foreach (var tagDirective in Constants.DefaultTagDirectives)
			{
				directives.Remove(tagDirective);
			}
			return directives;
		}

		// ReSharper disable UnusedParameter.Local
		private void AnalyzeVersionDirective(VersionDirective versionDirective)
		{
			if (versionDirective.Version.Major != Constants.MajorVersion || versionDirective.Version.Minor != Constants.MinorVersion)
			{
				throw new YamlException("Incompatible %YAML directive");
			}
		}
		// ReSharper restore UnusedParameter.Local

		private void AppendTagDirectiveTo(TagDirective value, bool allowDuplicates, TagDirectiveCollection tagDirectives)
		{
			if (tagDirectives.Contains(value))
			{
				if (!allowDuplicates)
				{
					throw new YamlException("Duplicate %TAG directive.");
				}
			}
			else
			{
				tagDirectives.Add(value);
			}
		}

		/// <summary>
		/// Expect the root node.
		/// </summary>
		private void EmitDocumentContent(ParsingEvent evt)
		{
			states.Push(EmitterState.DocumentEnd);
			EmitNode(evt, true, false, false);
		}

		/// <summary>
		/// Expect a node.
		/// </summary>
		private void EmitNode(ParsingEvent evt, bool isRoot, bool isMapping, bool isSimpleKey)
		{
			isRootContext = isRoot;
			isMappingContext = isMapping;
			isSimpleKeyContext = isSimpleKey;

			switch (evt.Type)
			{
				case EventType.Alias:
					EmitAlias();
					break;

				case EventType.Scalar:
					EmitScalar(evt);
					break;

				case EventType.SequenceStart:
					EmitSequenceStart(evt);
					break;

				case EventType.MappingStart:
					EmitMappingStart(evt);
					break;

				default:
					throw new YamlException(string.Format("Expected SCALAR, SEQUENCE-START, MAPPING-START, or ALIAS, got {0}", evt.Type));
			}
		}

		/// <summary>
		/// Expect ALIAS.
		/// </summary>
		private void EmitAlias()
		{
			ProcessAnchor();
			state = states.Pop();
		}

		/// <summary>
		/// Expect SCALAR.
		/// </summary>
		private void EmitScalar(ParsingEvent evt)
		{
			SelectScalarStyle(evt);
			ProcessAnchor();
			ProcessTag();
			IncreaseIndent(true, false);
			ProcessScalar();

			indent = indents.Pop();
			state = states.Pop();
		}

		private void SelectScalarStyle(ParsingEvent evt)
		{
			var scalar = (Scalar)evt;

			var style = scalar.Style;
			var noTag = tagData.handle == null && tagData.suffix == null;

			if (noTag && !scalar.IsPlainImplicit && !scalar.IsQuotedImplicit)
			{
				throw new YamlException("Neither tag nor isImplicit flags are specified.");
			}

			if (style == ScalarStyle.Any)
			{
				style = scalarData.isMultiline ? ScalarStyle.Folded : ScalarStyle.Plain;
			}

			if (isCanonical)
			{
				style = ScalarStyle.DoubleQuoted;
			}

			if (isSimpleKeyContext && scalarData.isMultiline)
			{
				style = ScalarStyle.DoubleQuoted;
			}

			if (style == ScalarStyle.Plain)
			{
				if ((flowLevel != 0 && !scalarData.isFlowPlainAllowed) || (flowLevel == 0 && !scalarData.isBlockPlainAllowed))
				{
					style = ScalarStyle.SingleQuoted;
				}
				if (string.IsNullOrEmpty(scalarData.value) && (flowLevel != 0 || isSimpleKeyContext))
				{
					style = ScalarStyle.SingleQuoted;
				}
				if (noTag && !scalar.IsPlainImplicit)
				{
					style = ScalarStyle.SingleQuoted;
				}
			}

			if (style == ScalarStyle.SingleQuoted)
			{
				if (!scalarData.isSingleQuotedAllowed)
				{
					style = ScalarStyle.DoubleQuoted;
				}
			}

			if (style == ScalarStyle.Literal || style == ScalarStyle.Folded)
			{
				if (!scalarData.isBlockAllowed || flowLevel != 0 || isSimpleKeyContext)
				{
					style = ScalarStyle.DoubleQuoted;
				}
			}

			scalarData.style = style;
		}

		private void ProcessScalar()
		{
			switch (scalarData.style)
			{
				case ScalarStyle.Plain:
					WritePlainScalar(scalarData.value, !isSimpleKeyContext);
					break;

				case ScalarStyle.SingleQuoted:
					WriteSingleQuotedScalar(scalarData.value, !isSimpleKeyContext);
					break;

				case ScalarStyle.DoubleQuoted:
					WriteDoubleQuotedScalar(scalarData.value, !isSimpleKeyContext);
					break;

				case ScalarStyle.Literal:
					WriteLiteralScalar(scalarData.value);
					break;

				case ScalarStyle.Folded:
					WriteFoldedScalar(scalarData.value);
					break;

				default:
					throw new InvalidOperationException();
			}
		}

		#region Write scalar Methods

		private void WritePlainScalar(string value, bool allowBreaks)
		{
			if (!isWhitespace)
			{
				Write(' ');
			}

			var previousSpace = false;
			var previousBreak = false;
			for (var index = 0; index < value.Length; ++index)
			{
				var character = value[index];

				if (IsSpace(character))
				{
					if (allowBreaks && !previousSpace && column > bestWidth && index + 1 < value.Length && value[index + 1] != ' ')
					{
						WriteIndent();
					}
					else
					{
						Write(character);
					}
					previousSpace = true;
				}
				else if (IsBreak(character))
				{
					if (!previousBreak && character == '\n')
					{
						WriteBreak();
					}
					WriteBreak();
					isIndentation = true;
					previousBreak = true;
				}
				else
				{
					if (previousBreak)
					{
						WriteIndent();
					}
					Write(character);
					isIndentation = false;
					previousSpace = false;
					previousBreak = false;
				}
			}

			isWhitespace = false;
			isIndentation = false;

			if (isRootContext)
			{
				isOpenEnded = true;
			}
		}

		private void WriteSingleQuotedScalar(string value, bool allowBreaks)
		{
			WriteIndicator("'", true, false, false);

			var previousSpace = false;
			var previousBreak = false;

			for (var index = 0; index < value.Length; ++index)
			{
				var character = value[index];

				if (character == ' ')
				{
					if (allowBreaks && !previousSpace && column > bestWidth && index != 0 && index + 1 < value.Length &&
						value[index + 1] != ' ')
					{
						WriteIndent();
					}
					else
					{
						Write(character);
					}
					previousSpace = true;
				}
				else if (IsBreak(character))
				{
					if (!previousBreak && character == '\n')
					{
						WriteBreak();
					}
					WriteBreak();
					isIndentation = true;
					previousBreak = true;
				}
				else
				{
					if (previousBreak)
					{
						WriteIndent();
					}
					if (character == '\'')
					{
						Write(character);
					}
					Write(character);
					isIndentation = false;
					previousSpace = false;
					previousBreak = false;
				}
			}

			WriteIndicator("'", false, false, false);

			isWhitespace = false;
			isIndentation = false;
		}

		private void WriteDoubleQuotedScalar(string value, bool allowBreaks)
		{
			WriteIndicator("\"", true, false, false);

			var previousSpace = false;
			for (var index = 0; index < value.Length; ++index)
			{
				var character = value[index];

				if (!IsPrintable(character) || IsBreak(character) || character == '"' || character == '\\')
				{
					Write('\\');

					switch (character)
					{
						case '\0':
							Write('0');
							break;

						case '\x7':
							Write('a');
							break;

						case '\x8':
							Write('b');
							break;

						case '\x9':
							Write('t');
							break;

						case '\xA':
							Write('n');
							break;

						case '\xB':
							Write('v');
							break;

						case '\xC':
							Write('f');
							break;

						case '\xD':
							Write('r');
							break;

						case '\x1B':
							Write('e');
							break;

						case '\x22':
							Write('"');
							break;

						case '\x5C':
							Write('\\');
							break;

						case '\x85':
							Write('N');
							break;

						case '\xA0':
							Write('_');
							break;

						case '\x2028':
							Write('L');
							break;

						case '\x2029':
							Write('P');
							break;

						default:
							var code = (short) character;
							if (code <= 0xFF)
							{
								Write('x');
								Write(code.ToString("X02", CultureInfo.InvariantCulture));
							}
							else
							{
								Write('u');
								Write(code.ToString("X04", CultureInfo.InvariantCulture));
							}
							break;
					}
					previousSpace = false;
				}
				else if (character == ' ')
				{
					if (allowBreaks && !previousSpace && column > bestWidth && index > 0 && index + 1 < value.Length)
					{
						WriteIndent();
						if (value[index + 1] == ' ')
						{
							Write('\\');
						}
					}
					else
					{
						Write(character);
					}
					previousSpace = true;
				}
				else
				{
					Write(character);
					previousSpace = false;
				}
			}

			WriteIndicator("\"", false, false, false);

			isWhitespace = false;
			isIndentation = false;
		}

		private void WriteLiteralScalar(string value)
		{
			var previousBreak = true;

			WriteIndicator("|", true, false, false);
			WriteBlockScalarHints(value);
			WriteBreak();

			isIndentation = true;
			isWhitespace = true;

			foreach (var character in value)
			{
				if (IsBreak(character))
				{
					WriteBreak();
					isIndentation = true;
					previousBreak = true;
				}
				else
				{
					if (previousBreak)
					{
						WriteIndent();
					}
					Write(character);
					isIndentation = false;
					previousBreak = false;
				}
			}
		}

		private void WriteFoldedScalar(string value)
		{
			var previousBreak = true;
			var leadingSpaces = true;

			WriteIndicator(">", true, false, false);
			WriteBlockScalarHints(value);
			WriteBreak();

			isIndentation = true;
			isWhitespace = true;

			for (var i = 0; i < value.Length; ++i)
			{
				var character = value[i];
				if (IsBreak(character))
				{
					if (!previousBreak && !leadingSpaces && character == '\n')
					{
						var k = 0;
						while (i + k < value.Length && IsBreak(value[i + k]))
						{
							++k;
						}
						if (i + k < value.Length && !(IsBlank(value[i + k]) || IsBreak(value[i + k])))
						{
							WriteBreak();
						}
					}
					WriteBreak();
					isIndentation = true;
					previousBreak = true;
				}
				else
				{
					if (previousBreak)
					{
						WriteIndent();
						leadingSpaces = IsBlank(character);
					}
					if (!previousBreak && character == ' ' && i + 1 < value.Length && value[i + 1] != ' ' && column > bestWidth)
					{
						WriteIndent();
					}
					else
					{
						Write(character);
					}
					isIndentation = false;
					previousBreak = false;
				}
			}
		}

		// Todo: isn't this what CharacterAnalyser is for?
		private static bool IsSpace(char character)
		{
			return character == ' ';
		}

		private static bool IsBreak(char character)
		{
			return character == '\r' || character == '\n' || character == '\x85' || character == '\x2028' ||
				character == '\x2029';
		}

		private static bool IsBlank(char character)
		{
			return character == ' ' || character == '\t';
		}

		private static bool IsPrintable(char character)
		{
			return
				character == '\x9' ||
					character == '\xA' ||
					character == '\xD' ||
					(character >= '\x20' && character <= '\x7E') ||
					character == '\x85' ||
					(character >= '\xA0' && character <= '\xD7FF') ||
					(character >= '\xE000' && character <= '\xFFFD');
		}

		#endregion

		/// <summary>
		/// Expect SEQUENCE-START.
		/// </summary>
		private void EmitSequenceStart(ParsingEvent evt)
		{
			ProcessAnchor();
			ProcessTag();

			var sequenceStart = (SequenceStart) evt;

			if (flowLevel != 0 || isCanonical || sequenceStart.Style == SequenceStyle.Flow || CheckEmptySequence())
			{
				state = EmitterState.FlowSequenceFirstItem;
			}
			else
			{
				state = EmitterState.BlockSequenceFirstItem;
			}
		}

		/// <summary>
		/// Expect MAPPING-START.
		/// </summary>
		private void EmitMappingStart(ParsingEvent evt)
		{
			ProcessAnchor();
			ProcessTag();

			var mappingStart = (MappingStart) evt;

			if (flowLevel != 0 || isCanonical || mappingStart.Style == MappingStyle.Flow || CheckEmptyMapping())
			{
				state = EmitterState.FlowMappingFirstKey;
			}
			else
			{
				state = EmitterState.BlockMappingFirstKey;
			}
		}

		private void ProcessAnchor()
		{
			if (anchorData.anchor != null)
			{
				WriteIndicator(anchorData.isAlias ? "*" : "&", true, false, false);
				WriteAnchor(anchorData.anchor);
			}
		}

		private void ProcessTag()
		{
			if (tagData.handle == null && tagData.suffix == null)
			{
				return;
			}

			if (tagData.handle != null)
			{
				WriteTagHandle(tagData.handle);
				if (tagData.suffix != null)
				{
					WriteTagContent(tagData.suffix, false);
				}
			}
			else
			{
				WriteIndicator("!<", true, false, false);
				WriteTagContent(tagData.suffix, false);
				WriteIndicator(">", false, false, false);
			}
		}

		/// <summary>
		/// Expect DOCUMENT-END.
		/// </summary>
		private void EmitDocumentEnd(ParsingEvent evt)
		{
			var documentEnd = evt as DocumentEnd;
			if (documentEnd != null)
			{
				WriteIndent();
				if (!documentEnd.IsImplicit)
				{
					WriteIndicator("...", true, false, false);
					WriteIndent();
					isDocumentEndWritten = true;
				}

				state = EmitterState.DocumentStart;

				tagDirectives.Clear();
			}
			else
			{
				throw new YamlException("Expected DOCUMENT-END.");
			}
		}

		/// <summary>
		/// Expect a flow item node.
		/// </summary>
		private void EmitFlowSequenceItem(ParsingEvent evt, bool isFirst)
		{
			if (isFirst)
			{
				WriteIndicator("[", true, true, false);
				IncreaseIndent(true, false);
				++flowLevel;
			}

			if (evt is SequenceEnd)
			{
				--flowLevel;
				indent = indents.Pop();
				if (isCanonical && !isFirst)
				{
					WriteIndicator(",", false, false, false);
					WriteIndent();
				}
				WriteIndicator("]", false, false, false);
				state = states.Pop();
				return;
			}

			if (!isFirst)
			{
				WriteIndicator(",", false, false, false);
			}

			if (isCanonical || column > bestWidth)
			{
				WriteIndent();
			}

			states.Push(EmitterState.FlowSequenceItem);

			EmitNode(evt, false, false, false);
		}

		/// <summary>
		/// Expect a flow key node.
		/// </summary>
		private void EmitFlowMappingKey(ParsingEvent evt, bool isFirst)
		{
			if (isFirst)
			{
				WriteIndicator("{", true, true, false);
				IncreaseIndent(true, false);
				++flowLevel;
			}

			if (evt is MappingEnd)
			{
				--flowLevel;
				indent = indents.Pop();
				if (isCanonical && !isFirst)
				{
					WriteIndicator(",", false, false, false);
					WriteIndent();
				}
				WriteIndicator("}", false, false, false);
				state = states.Pop();
				return;
			}

			if (!isFirst)
			{
				WriteIndicator(",", false, false, false);
			}
			if (isCanonical || column > bestWidth)
			{
				WriteIndent();
			}

			if (!isCanonical && CheckSimpleKey())
			{
				states.Push(EmitterState.FlowMappingSimpleValue);
				EmitNode(evt, false, true, true);
			}
			else
			{
				WriteIndicator("?", true, false, false);
				states.Push(EmitterState.FlowMappingValue);
				EmitNode(evt, false, true, false);
			}
		}

		/// <summary>
		/// Expect a flow value node.
		/// </summary>
		private void EmitFlowMappingValue(ParsingEvent evt, bool isSimple)
		{
			if (isSimple)
			{
				WriteIndicator(":", false, false, false);
			}
			else
			{
				if (isCanonical || column > bestWidth)
				{
					WriteIndent();
				}
				WriteIndicator(":", true, false, false);
			}
			states.Push(EmitterState.FlowMappingKey);
			EmitNode(evt, false, true, false);
		}

		/// <summary>
		/// Expect a block item node.
		/// </summary>
		private void EmitBlockSequenceItem(ParsingEvent evt, bool isFirst)
		{
			if (isFirst)
			{
				IncreaseIndent(false, (isMappingContext && !isIndentation));
			}

			if (evt is SequenceEnd)
			{
				indent = indents.Pop();
				state = states.Pop();
				return;
			}

			WriteIndent();
			WriteIndicator("-", true, false, true);
			states.Push(EmitterState.BlockSequenceItem);

			EmitNode(evt, false, false, false);
		}

		/// <summary>
		/// Expect a block key node.
		/// </summary>
		private void EmitBlockMappingKey(ParsingEvent evt, bool isFirst)
		{
			if (isFirst)
			{
				IncreaseIndent(false, false);
			}

			if (evt is MappingEnd)
			{
				indent = indents.Pop();
				state = states.Pop();
				return;
			}

			WriteIndent();

			if (CheckSimpleKey())
			{
				states.Push(EmitterState.BlockMappingSimpleValue);
				EmitNode(evt, false, true, true);
			}
			else
			{
				WriteIndicator("?", true, false, true);
				states.Push(EmitterState.BlockMappingValue);
				EmitNode(evt, false, true, false);
			}
		}

		/// <summary>
		/// Expect a block value node.
		/// </summary>
		private void EmitBlockMappingValue(ParsingEvent evt, bool isSimple)
		{
			if (isSimple)
			{
				WriteIndicator(":", false, false, false);
			}
			else
			{
				WriteIndent();
				WriteIndicator(":", true, false, true);
			}
			states.Push(EmitterState.BlockMappingKey);
			EmitNode(evt, false, true, false);
		}

		private void IncreaseIndent(bool isFlow, bool isIndentless)
		{
			indents.Push(indent);

			if (indent < 0)
			{
				indent = isFlow ? bestIndent : 0;
			}
			else if (!isIndentless)
			{
				indent += bestIndent;
			}
		}

		#region Check Methods

		/// <summary>
		/// Check if the document content is an empty scalar.
		/// </summary>
		private bool CheckEmptyDocument()
		{
			var index = 0;
			foreach (var parsingEvent in events)
			{
				index++;
				if (index == 2)
				{
					var scalar = parsingEvent as Scalar;
					if (scalar != null)
					{
						return string.IsNullOrEmpty(scalar.Value);
					}
					break;
				}
			}

			return false;
		}

		/// <summary>
		/// Check if the next node can be expressed as a simple key.
		/// </summary>
		private bool CheckSimpleKey()
		{
			if (events.Count < 1)
			{
				return false;
			}

			int length;
			switch (events.Peek().Type)
			{
			case EventType.Alias:
				length = SafeStringLength(anchorData.anchor);
				break;

			case EventType.Scalar:
				if (scalarData.isMultiline)
				{
					return false;
				}

				length =
					SafeStringLength(anchorData.anchor) +
						SafeStringLength(tagData.handle) +
						SafeStringLength(tagData.suffix) +
						SafeStringLength(scalarData.value);
				break;

			case EventType.SequenceStart:
				if (!CheckEmptySequence())
				{
					return false;
				}
				length =
					SafeStringLength(anchorData.anchor) +
						SafeStringLength(tagData.handle) +
						SafeStringLength(tagData.suffix);
				break;

			case EventType.MappingStart:
				if (!CheckEmptySequence())
				{
					return false;
				}
				length =
					SafeStringLength(anchorData.anchor) +
						SafeStringLength(tagData.handle) +
						SafeStringLength(tagData.suffix);
				break;

			default:
				return false;
			}

			return length <= MaxAliasLength;
		}

		private int SafeStringLength(string value)
		{
			return value == null? 0: value.Length;
		}

		private bool CheckEmptySequence()
		{
			if (events.Count < 2)
			{
				return false;
			}

			// Todo: must be something better than this FakeList
			var eventList = new FakeList<ParsingEvent>(events);
			return eventList[0] is SequenceStart && eventList[1] is SequenceEnd;
		}

		private bool CheckEmptyMapping()
		{
			if (events.Count < 2)
			{
				return false;
			}

			// Todo: must be something better than this FakeList
			var eventList = new FakeList<ParsingEvent>(events);
			return eventList[0] is MappingStart && eventList[1] is MappingEnd;
		}

		#endregion

		#region Write Methods

		private void WriteBlockScalarHints(string value)
		{
			var analyzer = new CharacterAnalyzer<StringLookAheadBuffer>(new StringLookAheadBuffer(value));

			if (analyzer.IsSpace() || analyzer.IsBreak())
			{
				var indentHint = string.Format(CultureInfo.InvariantCulture, "{0}\0", bestIndent);
				WriteIndicator(indentHint, false, false, false);
			}

			isOpenEnded = false;

			string chompHint = null;
			if (value.Length == 0 || !analyzer.IsBreak(value.Length - 1))
			{
				chompHint = "-";
			}
			else if (value.Length >= 2 && analyzer.IsBreak(value.Length - 2))
			{
				chompHint = "+";
				isOpenEnded = true;
			}

			if (chompHint != null)
			{
				WriteIndicator(chompHint, false, false, false);
			}
		}

		private void WriteIndicator(string indicator, bool needWhitespace, bool whitespace, bool indentation)
		{
			if (needWhitespace && !isWhitespace)
			{
				Write(' ');
			}

			Write(indicator);

			isWhitespace = whitespace;
			isIndentation &= indentation;
			isOpenEnded = false;
		}

		private void WriteIndent()
		{
			var currentIndent = Math.Max(indent, 0);

			if (!isIndentation || column > currentIndent || (column == currentIndent && !isWhitespace))
			{
				WriteBreak();
			}

			while (column < currentIndent)
			{
				Write(' ');
			}

			isWhitespace = true;
			isIndentation = true;
		}

		private void WriteAnchor(string value)
		{
			Write(value);

			isWhitespace = false;
			isIndentation = false;
		}

		private void WriteTagHandle(string value)
		{
			if (!isWhitespace)
			{
				Write(' ');
			}

			Write(value);

			isWhitespace = false;
			isIndentation = false;
		}

		private void WriteTagContent(string value, bool needsWhitespace)
		{
			if (needsWhitespace && !isWhitespace)
			{
				Write(' ');
			}

			Write(UrlEncode(value));

			isWhitespace = false;
			isIndentation = false;
		}

		private string UrlEncode(string text)
		{
			return uriReplacer.Replace(text, delegate(Match match) {
				var buffer = new StringBuilder();
				foreach (var toEncode in Encoding.UTF8.GetBytes(match.Value))
				{
					buffer.AppendFormat("%{0:X02}", toEncode);
				}
				return buffer.ToString();
			});
		}

		private void Write(char value)
		{
			output.Write(value);
			++column;
		}

		private void Write(string value)
		{
			output.Write(value);
			column += value.Length;
		}

		private void WriteBreak()
		{
			output.WriteLine();
			column = 0;
		}

		#endregion
	}
}
