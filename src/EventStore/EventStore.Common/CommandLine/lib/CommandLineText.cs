#region License
//
// Command Line Library: CommandLineText.cs
//
// Author:
//   Giacomo Stelluti Scala (gsscoder@gmail.com)
// Contributor(s):
//   Steven Evans
//
// Copyright (C) 2005 - 2012 Giacomo Stelluti Scala
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
#endregion
#region Edit Note
// source code copied from https://github.com/gsscoder/commandline 08.08.2012 on revision https://github.com/gsscoder/commandline/commit/1e2e01429d46418b0378fb1bb859251a9898b6da of branch master
// original file:  https://github.com/gsscoder/commandline/commit/3ac8b63ef44de1bd52e8aca93bb7a2eb274c638c#src/libcmdline/CommandLineText.cs
#endregion
#region Using Directives


using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;


#endregion

namespace EventStore.Common.CommandLine.lib
{
    #region *SentenceBuilder
    /// <summary>
    /// Models an abstract sentence builder.
    /// </summary>
    public abstract class BaseSentenceBuilder
    {
        /// <summary>
        /// Creates the built in sentence builder.
        /// </summary>
        /// <returns>
        /// The built in sentence builder.
        /// </returns>
        public static BaseSentenceBuilder CreateBuiltIn()
        {
            return new EnglishSentenceBuilder();
        }

        /// <summary>
        /// Gets a string containing word 'option'.
        /// </summary>
        /// <value>
        /// The word 'option'.
        /// </value>
        public abstract string OptionWord { get; }

        /// <summary>
        /// Gets a string containing the word 'and'.
        /// </summary>
        /// <value>
        /// The word 'and'.
        /// </value>
        public abstract string AndWord { get; }

        /// <summary>
        /// Gets a string containing the sentence 'required option missing'.
        /// </summary>
        /// <value>
        /// The sentence 'required option missing'.
        /// </value>
        public abstract string RequiredOptionMissingText { get; }

        /// <summary>
        /// Gets a string containing the sentence 'violates format'.
        /// </summary>
        /// <value>
        /// The sentence 'violates format'.
        /// </value>
        public abstract string ViolatesFormatText { get; }

        /// <summary>
        /// Gets a string containing the sentence 'violates mutual exclusiveness'.
        /// </summary>
        /// <value>
        /// The sentence 'violates mutual exclusiveness'.
        /// </value>
        public abstract string ViolatesMutualExclusivenessText { get; }

        public abstract string IsNotRecognized { get; }

        /// <summary>
        /// Gets a string containing the error heading text.
        /// </summary>
        /// <value>
        /// The error heading text.
        /// </value>
        public abstract string ErrorsHeadingText { get; }
    }

    /// <summary>
    /// Models an english sentence builder, currently the default one.
    /// </summary>
    public class EnglishSentenceBuilder : BaseSentenceBuilder
    {
        /// <summary>
        /// Gets a string containing word 'option' in english.
        /// </summary>
        /// <value>
        /// The word 'option' in english.
        /// </value>
        public override string OptionWord { get { return "option"; } }

        /// <summary>
        /// Gets a string containing the word 'and' in english.
        /// </summary>
        /// <value>
        /// The word 'and' in english.
        /// </value>        
        public override string AndWord { get { return "and"; } }

        /// <summary>
        /// Gets a string containing the sentence 'required option missing' in english.
        /// </summary>
        /// <value>
        /// The sentence 'required option missing' in english.
        /// </value>        
        public override string RequiredOptionMissingText { get { return "required option is missing"; } }

        /// <summary>
        /// Gets a string containing the sentence 'violates format' in english.
        /// </summary>
        /// <value>
        /// The sentence 'violates format' in english.
        /// </value>        
        public override string ViolatesFormatText { get { return "violates format"; } }

        /// <summary>
        /// Gets a string containing the sentence 'violates mutual exclusiveness' in english.
        /// </summary>
        /// <value>
        /// The sentence 'violates mutual exclusiveness' in english.
        /// </value>        
        public override string ViolatesMutualExclusivenessText { get { return "violates mutual exclusiveness"; } }

        public override string IsNotRecognized { get { return "is not recognized"; } }

        /// <summary>
        /// Gets a string containing the error heading text in english.
        /// </summary>
        /// <value>
        /// The error heading text in english.
        /// </value>
        public override string ErrorsHeadingText { get { return "ERROR(S):"; } }


    }
    #endregion

    #region Secondary Helpers
    /// <summary>
    /// Models the copyright informations part of an help text.
    /// You can assign it where you assign any <see cref="System.String"/> instance.
    /// </summary>
    public class CopyrightInfo
    {
        private readonly bool _isSymbolUpper;
        private readonly int[] _years;
        private readonly string _author;
        private static readonly string _defaultCopyrightWord = "Copyright";
        private static readonly string _symbolLower = "(c)";
        private static readonly string _symbolUpper = "(C)";
        private StringBuilder _builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyrightInfo"/> class.
        /// </summary>
        protected CopyrightInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyrightInfo"/> class
        /// specifying author and year.
        /// </summary>
        /// <param name="author">The company or person holding the copyright.</param>
        /// <param name="year">The year of coverage of copyright.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="author"/> is null or empty string.</exception>
        public CopyrightInfo(string author, int year)
            : this(true, author, new int[] { year })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyrightInfo"/> class
        /// specifying author and years.
        /// </summary>
        /// <param name="author">The company or person holding the copyright.</param>
        /// <param name="years">The years of coverage of copyright.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="author"/> is null or empty string.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when parameter <paramref name="years"/> is not supplied.</exception>
        public CopyrightInfo(string author, params int[] years)
            : this(true, author, years)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyrightInfo"/> class
        /// specifying symbol case, author and years.
        /// </summary>
        /// <param name="isSymbolUpper">The case of the copyright symbol.</param>
        /// <param name="author">The company or person holding the copyright.</param>
        /// <param name="years">The years of coverage of copyright.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="author"/> is null or empty string.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when parameter <paramref name="years"/> is not supplied.</exception>
        public CopyrightInfo(bool isSymbolUpper, string author, params int[] years)
        {
            Assumes.NotNullOrEmpty(author, "author");
            Assumes.NotZeroLength(years, "years");

            const int extraLength = 10;
            _isSymbolUpper = isSymbolUpper;
            _author = author;
            _years = years;
            _builder = new StringBuilder
                    (CopyrightWord.Length + author.Length + (4 * years.Length) + extraLength);
        }

        /// <summary>
        /// Returns the copyright informations as a <see cref="System.String"/>.
        /// </summary>
        /// <returns>The <see cref="System.String"/> that contains the copyright informations.</returns>
        public override string ToString()
        {
            _builder.Append(CopyrightWord);
            _builder.Append(' ');
            if (_isSymbolUpper)
                _builder.Append(_symbolUpper);
            else
                _builder.Append(_symbolLower);

            _builder.Append(' ');
            _builder.Append(FormatYears(_years));
            _builder.Append(' ');
            _builder.Append(_author);

            return _builder.ToString();
        }

        /// <summary>
        /// Converts the copyright informations to a <see cref="System.String"/>.
        /// </summary>
        /// <param name="info">This <see cref="CopyrightInfo"/> instance.</param>
        /// <returns>The <see cref="System.String"/> that contains the copyright informations.</returns>
        public static implicit operator string(CopyrightInfo info)
        {
            return info.ToString();
        }

        /// <summary>
        /// When overridden in a derived class, allows to specify a different copyright word.
        /// </summary>
        protected virtual string CopyrightWord
        {
            get { return _defaultCopyrightWord; }
        }

        /// <summary>
        /// When overridden in a derived class, allows to specify a new algorithm to render copyright years
        /// as a <see cref="System.String"/> instance.
        /// </summary>
        /// <param name="years">A <see cref="System.Int32"/> array of years.</param>
        /// <returns>A <see cref="System.String"/> instance with copyright years.</returns>
        protected virtual string FormatYears(int[] years)
        {
            if (years.Length == 1)
                return years[0].ToString(CultureInfo.InvariantCulture);

            var yearsPart = new StringBuilder(years.Length * 6);
            for (int i = 0; i < years.Length; i++)
            {
                yearsPart.Append(years[i].ToString(CultureInfo.InvariantCulture));
                int next = i + 1;
                if (next < years.Length)
                {
                    if (years[next] - years[i] > 1)
                        yearsPart.Append(" - ");
                    else
                        yearsPart.Append(", ");
                }
            }

            return yearsPart.ToString();
        }
    }

    /// <summary>
    /// Models the heading informations part of an help text.
    /// You can assign it where you assign any <see cref="System.String"/> instance.
    /// </summary>
    public class HeadingInfo
    {
        private readonly string _programName;
        private readonly string _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeadingInfo"/> class
        /// specifying program name.
        /// </summary>
        /// <param name="programName">The name of the program.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="programName"/> is null or empty string.</exception>
        public HeadingInfo(string programName)
            : this(programName, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeadingInfo"/> class
        /// specifying program name and version.
        /// </summary>
        /// <param name="programName">The name of the program.</param>
        /// <param name="version">The version of the program.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="programName"/> is null or empty string.</exception>
        public HeadingInfo(string programName, string version)
        {
            Assumes.NotNullOrEmpty(programName, "programName");

            _programName = programName;
            _version = version;
        }

        /// <summary>
        /// Returns the heading informations as a <see cref="System.String"/>.
        /// </summary>
        /// <returns>The <see cref="System.String"/> that contains the heading informations.</returns>
        public override string ToString()
        {
            bool isVersionNull = string.IsNullOrEmpty(_version);
            var builder = new StringBuilder(_programName.Length +
                (!isVersionNull ? _version.Length + 1 : 0)
            );
            builder.Append(_programName);
            if (!isVersionNull)
            {
                builder.Append(' ');
                builder.Append(_version);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts the heading informations to a <see cref="System.String"/>.
        /// </summary>
        /// <param name="info">This <see cref="HeadingInfo"/> instance.</param>
        /// <returns>The <see cref="System.String"/> that contains the heading informations.</returns>
        public static implicit operator string(HeadingInfo info)
        {
            return info.ToString();
        }

        /// <summary>
        /// Writes out a string and a new line using the program name specified in the constructor
        /// and <paramref name="message"/> parameter.
        /// </summary>
        /// <param name="message">The <see cref="System.String"/> message to write.</param>
        /// <param name="writer">The target <see cref="System.IO.TextWriter"/> derived type.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="message"/> is null or empty string.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="writer"/> is null.</exception>
        public void WriteMessage(string message, TextWriter writer)
        {
            Assumes.NotNullOrEmpty(message, "message");
            Assumes.NotNull(writer, "writer");

            var builder = new StringBuilder(_programName.Length + message.Length + 2);
            builder.Append(_programName);
            builder.Append(": ");
            builder.Append(message);
            writer.WriteLine(builder.ToString());
        }

        /// <summary>
        /// Writes out a string and a new line using the program name specified in the constructor
        /// and <paramref name="message"/> parameter to standard output stream.
        /// </summary>
        /// <param name="message">The <see cref="System.String"/> message to write.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="message"/> is null or empty string.</exception>
        public void WriteMessage(string message)
        {
            WriteMessage(message, Console.Out);
        }

        /// <summary>
        /// Writes out a string and a new line using the program name specified in the constructor
        /// and <paramref name="message"/> parameter to standard error stream.
        /// </summary>
        /// <param name="message">The <see cref="System.String"/> message to write.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="message"/> is null or empty string.</exception>
        public void WriteError(string message)
        {
            WriteMessage(message, Console.Error);
        }
    }

    /*
    internal static class AssemblyVersionAttributeUtil
    {
        public static string GetInformationalVersion(this AssemblyVersionAttribute attr)
        {
            var parts = attr.Version.Split('.');
            if (parts.Length > 2)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", parts[0], parts[1]);
            }
            return attr.Version;
        }
    }
    */

    /*
    internal static class StringBuilderUtil
    {
        public static void AppendLineIfNotNullOrEmpty(this StringBuilder bldr, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                bldr.AppendLine(value);
            }
        }
    }
    */
    #endregion

    #region Attributes
    public abstract class MultiLineTextAttribute : Attribute
    {
        //string _fullText;
        string _line1;
        string _line2;
        string _line3;
        string _line4;
        string _line5;

        //public MultiLineTextAttribute(object fullText)
        //{
        //    var realFullText = fullText as string;
        //    Assumes.NotNullOrEmpty(realFullText, "fullText");
        //    _fullText = realFullText;
        //}
        public MultiLineTextAttribute(string line1)
        {
            Assumes.NotNullOrEmpty(line1, "line1");
            _line1 = line1;
        }
        public MultiLineTextAttribute(string line1, string line2)
            : this(line1)
        {
            Assumes.NotNullOrEmpty(line2, "line2");
            _line2 = line2;
        }
        public MultiLineTextAttribute(string line1, string line2, string line3)
            : this(line1, line2)
        {
            Assumes.NotNullOrEmpty(line3, "line3");
            _line3 = line3;
        }
        public MultiLineTextAttribute(string line1, string line2, string line3, string line4)
            : this(line1, line2, line3)
        {
            Assumes.NotNullOrEmpty(line4, "line4");
            _line4 = line4;
        }
        public MultiLineTextAttribute(string line1, string line2, string line3, string line4, string line5)
            : this(line1, line2, line3, line4)
        {
            Assumes.NotNullOrEmpty(line5, "line5");
            _line5 = line5;
        }

        /*
        public string FullText
        {
            get
            {
                if (string.IsNullOrEmpty(_fullText))
                {
                    if (!string.IsNullOrEmpty(_line1))
                    {
                        var builder = new StringBuilder(_line1);
                        builder.AppendLineIfNotNullOrEmpty(_line2);
                        builder.AppendLineIfNotNullOrEmpty(_line3);
                        builder.AppendLineIfNotNullOrEmpty(_line4);
                        builder.AppendLineIfNotNullOrEmpty(_line5);
                        _fullText = builder.ToString();
                    }
                }
                return _fullText;
            }
        }
        */

        internal void AddToHelpText(HelpText helpText, bool before)
        {
            if (before)
            {
                if (!string.IsNullOrEmpty(_line1)) helpText.AddPreOptionsLine(_line1);
                if (!string.IsNullOrEmpty(_line2)) helpText.AddPreOptionsLine(_line2);
                if (!string.IsNullOrEmpty(_line3)) helpText.AddPreOptionsLine(_line3);
                if (!string.IsNullOrEmpty(_line4)) helpText.AddPreOptionsLine(_line4);
                if (!string.IsNullOrEmpty(_line5)) helpText.AddPreOptionsLine(_line5);
            }
            else
            {
                if (!string.IsNullOrEmpty(_line1)) helpText.AddPostOptionsLine(_line1);
                if (!string.IsNullOrEmpty(_line2)) helpText.AddPostOptionsLine(_line2);
                if (!string.IsNullOrEmpty(_line3)) helpText.AddPostOptionsLine(_line3);
                if (!string.IsNullOrEmpty(_line4)) helpText.AddPostOptionsLine(_line4);
                if (!string.IsNullOrEmpty(_line5)) helpText.AddPostOptionsLine(_line5);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false), ComVisible(true)]
    public sealed class AssemblyLicenseAttribute : MultiLineTextAttribute
    {
        //public AssemblyLicenseAttribute(object fullText)
        //    : base(fullText)
        //{
        //}
        public AssemblyLicenseAttribute(string line1)
            : base(line1)
        {
        }
        public AssemblyLicenseAttribute(string line1, string line2)
            : base(line1, line2)
        {
        }
        public AssemblyLicenseAttribute(string line1, string line2, string line3)
            : base(line1, line2, line3)
        {
        }
        public AssemblyLicenseAttribute(string line1, string line2, string line3, string line4)
            : base(line1, line2, line3, line4)
        {
        }
        public AssemblyLicenseAttribute(string line1, string line2, string line3, string line4, string line5)
            : base(line1, line2, line3, line4, line5)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false), ComVisible(true)]
    public sealed class AssemblyUsageAttribute : MultiLineTextAttribute
    {
        //public AssemblyUsageAttribute(object fullText)
        //    : base(fullText)
        //{
        //}
        public AssemblyUsageAttribute(string line1)
            : base(line1)
        {
        }
        public AssemblyUsageAttribute(string line1, string line2)
            : base(line1, line2)
        {
        }
        public AssemblyUsageAttribute(string line1, string line2, string line3)
            : base(line1, line2, line3)
        {
        }
        public AssemblyUsageAttribute(string line1, string line2, string line3, string line4)
            : base(line1, line2, line3, line4)
        {
        }
        public AssemblyUsageAttribute(string line1, string line2, string line3, string line4, string line5)
            : base(line1, line2, line3, line4, line5)
        {
        }
    }

    /*[AttributeUsage(AttributeTargets.Assembly, Inherited=false), ComVisible(true)]
    public sealed class AssemblyInformationalVersionAttribute : Attribute
    {
        public string Version { get; private set; }

        public AssemblyInformationalVersionAttribute (string version)
        {
            this.Version = version;
        }
    }*/
    #endregion

    #region HelpText
    /// <summary>
    /// Handle parsing errors delegate.
    /// </summary>
    public delegate void HandleParsingErrorsDelegate(HelpText current);

    /// <summary>
    /// Provides data for the FormatOptionHelpText event.
    /// </summary>
    public class FormatOptionHelpTextEventArgs : EventArgs
    {
        private readonly BaseOptionAttribute _option;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormatOptionHelpTextEventArgs"/> class.
        /// </summary>
        /// <param name="option">Option to format.</param>
        public FormatOptionHelpTextEventArgs(BaseOptionAttribute option)
        {
            _option = option;
        }

        /// <summary>
        /// Gets the option to format.
        /// </summary>
        public BaseOptionAttribute Option
        {
            get
            {
                return _option;
            }
        }
    }

    /// <summary>
    /// Models an help text and collects related informations.
    /// You can assign it in place of a <see cref="System.String"/> instance.
    /// </summary>
    public class HelpText
    {
        private const int _builderCapacity = 128;
        private const int _defaultMaximumLength = 80; // default console width
        private int? _maximumDisplayWidth;
        private string _heading;
        private string _copyright;
        private bool _additionalNewLineAfterOption;
        private StringBuilder _preOptionsHelp;
        private StringBuilder _optionsHelp;
        private StringBuilder _postOptionsHelp;
        private BaseSentenceBuilder _sentenceBuilder;
        private static readonly string _defaultRequiredWord = "Required.";
        private bool _addDashesToOption = false;

        /// <summary>
        /// Occurs when an option help text is formatted.
        /// </summary>
        public event EventHandler<FormatOptionHelpTextEventArgs> FormatOptionHelpText;

        /// <summary>
        /// Message type enumeration.
        /// </summary>
        public enum MessageEnum : short
        {
            /// <summary>
            /// Parsing error due to a violation of the format of an option value.
            /// </summary>
            ParsingErrorViolatesFormat,
            /// <summary>
            /// Parsing error due to a violation of mandatory option.
            /// </summary>
            ParsingErrorViolatesRequired,
            /// <summary>
            /// Parsing error due to a violation of the mutual exclusiveness of two or more option.
            /// </summary>
            ParsingErrorViolatesExclusiveness
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class.
        /// </summary>
        public HelpText()
        {
            _preOptionsHelp = new StringBuilder(_builderCapacity);
            _postOptionsHelp = new StringBuilder(_builderCapacity);

            _sentenceBuilder = BaseSentenceBuilder.CreateBuiltIn();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class 
        /// specifying the sentence builder.
        /// </summary>
        /// <param name="sentenceBuilder">
        /// A <see cref="BaseSentenceBuilder"/> instance.
        /// </param>
        public HelpText(BaseSentenceBuilder sentenceBuilder)
            : this()
        {
            Assumes.NotNull(sentenceBuilder, "sentenceBuilder");

            _sentenceBuilder = sentenceBuilder;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class
        /// specifying heading informations.
        /// </summary>
        /// <param name="heading">A string with heading information or
        /// an instance of <see cref="HeadingInfo"/>.</param>
        /// <exception cref="System.ArgumentException">Thrown when parameter <paramref name="heading"/> is null or empty string.</exception>
        public HelpText(string heading)
            : this()
        {
            Assumes.NotNullOrEmpty(heading, "heading");

            _heading = heading;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class
        /// specifying the sentence builder and heading informations.
        /// </summary>
        /// <param name="sentenceBuilder">
        /// A <see cref="BaseSentenceBuilder"/> instance.
        /// </param>
        /// <param name="heading">
        /// A string with heading information or
        /// an instance of <see cref="HeadingInfo"/>.
        /// </param>
        public HelpText(BaseSentenceBuilder sentenceBuilder, string heading)
            : this(heading)
        {
            Assumes.NotNull(sentenceBuilder, "sentenceBuilder");

            _sentenceBuilder = sentenceBuilder;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class
        /// specifying heading and copyright informations.
        /// </summary>
        /// <param name="heading">A string with heading information or
        /// an instance of <see cref="HeadingInfo"/>.</param>
        /// <param name="copyright">A string with copyright information or
        /// an instance of <see cref="CopyrightInfo"/>.</param>
        /// <exception cref="System.ArgumentException">Thrown when one or more parameters <paramref name="heading"/> are null or empty strings.</exception>
        public HelpText(string heading, string copyright)
            : this()
        {
            Assumes.NotNullOrEmpty(heading, "heading");
            Assumes.NotNullOrEmpty(copyright, "copyright");

            _heading = heading;
            _copyright = copyright;
        }

        public HelpText(BaseSentenceBuilder sentenceBuilder, string heading, string copyright)
            : this(heading, copyright)
        {
            Assumes.NotNull(sentenceBuilder, "sentenceBuilder");

            _sentenceBuilder = sentenceBuilder;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpText"/> class
        /// specifying heading and copyright informations.
        /// </summary>
        /// <param name="heading">A string with heading information or
        /// an instance of <see cref="HeadingInfo"/>.</param>
        /// <param name="copyright">A string with copyright information or
        /// an instance of <see cref="CopyrightInfo"/>.</param>
        /// <param name="options">The instance that collected command line arguments parsed with <see cref="CommandLineParser"/> class.</param>
        /// <exception cref="System.ArgumentException">Thrown when one or more parameters <paramref name="heading"/> are null or empty strings.</exception>
        public HelpText(string heading, string copyright, object options)
            : this()
        {
            Assumes.NotNullOrEmpty(heading, "heading");
            Assumes.NotNullOrEmpty(copyright, "copyright");
            Assumes.NotNull(options, "options");

            _heading = heading;
            _copyright = copyright;
            AddOptions(options);
        }

        public HelpText(BaseSentenceBuilder sentenceBuilder, string heading, string copyright, object options)
            : this(heading, copyright, options)
        {
            Assumes.NotNull(sentenceBuilder, "sentenceBuilder");

            _sentenceBuilder = sentenceBuilder;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="HelpText"/> class using common defaults.
        /// </summary>
        /// <returns>
        /// An instance of <see cref="HelpText"/> class.
        /// </returns>
        /// <param name='options'>
        /// The instance that collected command line arguments parsed with <see cref="CommandLineParser"/> class.
        /// </param>
        public static HelpText AutoBuild(object options)
        {
            return AutoBuild(options, null);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="HelpText"/> class using common defaults.
        /// </summary>
        /// <returns>
        /// An instance of <see cref="HelpText"/> class.
        /// </returns>
        /// <param name='options'>
        /// The instance that collected command line arguments parsed with <see cref="CommandLineParser"/> class.
        /// </param>
        /// <param name='errDelegate'>
        /// A delegate used to customize the text block for reporting parsing errors.
        /// </param>
        public static HelpText AutoBuild(object options, HandleParsingErrorsDelegate errDelegate)
        {
            var title = ReflectionUtil.GetAttribute<AssemblyTitleAttribute>();
            if (title == null) throw new InvalidOperationException("HelpText::AutoBuild() requires that you define AssemblyTitleAttribute.");
            var copyright = ReflectionUtil.GetAttribute<AssemblyCopyrightAttribute>();
            if (copyright == null) throw new InvalidOperationException("HelpText::AutoBuild() requires that you define AssemblyCopyrightAttribute.");
            var version = ReflectionUtil.GetAttribute<AssemblyInformationalVersionAttribute>();
            // let AssemblyInformationalVersionAttribute be null because its not added to project by default and its not so crucial

            var auto = new HelpText
            {
                Heading = new HeadingInfo(Path.GetFileNameWithoutExtension(title.Title), version != null ? version.InformationalVersion : "(unspecified)"),
                Copyright = copyright.Copyright,
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            if (errDelegate != null)
            {
                var typedTarget = options as CommandLineOptionsBase;
                if (typedTarget != null)
                {
                    errDelegate(auto);
                }
            }

            var license = ReflectionUtil.GetAttribute<AssemblyLicenseAttribute>();
            if (license != null)
            {
                //auto.AddPreOptionsLine(license.FullText);
                license.AddToHelpText(auto, true);
            }
            var usage = ReflectionUtil.GetAttribute<AssemblyUsageAttribute>();
            if (usage != null)
            {
                //auto.AddPreOptionsLine(usage.FullText);
                usage.AddToHelpText(auto, true);
            }

            auto.AddOptions(options);

            return auto;
        }

        public static void DefaultParsingErrorsHandler(CommandLineOptionsBase options, HelpText current)
        {
            if (options.InternalLastPostParsingState.Errors.Count > 0)
            {
                var errors = current.RenderParsingErrorsText(options, 2); // indent with two spaces
                if (!string.IsNullOrEmpty(errors))
                {
                    current.AddPreOptionsLine(string.Concat(Environment.NewLine, current.SentenceBuilder.ErrorsHeadingText));
                    //current.AddPreOptionsLine(errors);
                    var lines = errors.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (var line in lines) { current.AddPreOptionsLine(line); }
                }
            }
        }

        /// <summary>
        /// Sets the heading information string.
        /// You can directly assign a <see cref="HeadingInfo"/> instance.
        /// </summary>
        public string Heading
        {
            set
            {
                Assumes.NotNullOrEmpty(value, "value");
                _heading = value;
            }
        }

        /// <summary>
        /// Sets the copyright information string.
        /// You can directly assign a <see cref="CopyrightInfo"/> instance.
        /// </summary>
        public string Copyright
        {
            set
            {
                Assumes.NotNullOrEmpty(value, "value");
                _copyright = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum width of the display.  This determines word wrap when displaying the text.
        /// </summary>
        /// <value>The maximum width of the display.</value>
        public int MaximumDisplayWidth
        {
            get { return _maximumDisplayWidth.HasValue ? _maximumDisplayWidth.Value : _defaultMaximumLength; }
            set { _maximumDisplayWidth = value; }
        }

        /// <summary>
        /// Gets or sets the format of options for adding or removing dashes.
        /// It modifies behavior of <see cref="AddOptions"/> method.
        /// </summary>
        public bool AddDashesToOption
        {
            get { return _addDashesToOption; }
            set { _addDashesToOption = value; }
        }

        /// <summary>
        /// Gets or sets whether to add an additional line after the description of the option.
        /// </summary>
        public bool AdditionalNewLineAfterOption
        {
            get { return _additionalNewLineAfterOption; }
            set { _additionalNewLineAfterOption = value; }
        }

        public BaseSentenceBuilder SentenceBuilder
        {
            get { return _sentenceBuilder; }
        }

        /// <summary>
        /// Adds a text line after copyright and before options usage informations.
        /// </summary>
        /// <param name="value">A <see cref="System.String"/> instance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="value"/> is null or empty string.</exception>
        public void AddPreOptionsLine(string value)
        {
            AddPreOptionsLine(value, MaximumDisplayWidth);
        }

        private void AddPreOptionsLine(string value, int maximumLength)
        {
            AddLine(_preOptionsHelp, value, maximumLength);
        }

        /// <summary>
        /// Adds a text line at the bottom, after options usage informations.
        /// </summary>
        /// <param name="value">A <see cref="System.String"/> instance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="value"/> is null or empty string.</exception>
        public void AddPostOptionsLine(string value)
        {
            AddLine(_postOptionsHelp, value);
        }

        /// <summary>
        /// Adds a text block with options usage informations.
        /// </summary>
        /// <param name="options">The instance that collected command line arguments parsed with <see cref="CommandLineParser"/> class.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="options"/> is null.</exception>
        public void AddOptions(object options)
        {
            AddOptions(options, _defaultRequiredWord);
        }

        /// <summary>
        /// Adds a text block with options usage informations.
        /// </summary>
        /// <param name="options">The instance that collected command line arguments parsed with the <see cref="CommandLineParser"/> class.</param>
        /// <param name="requiredWord">The word to use when the option is required.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="options"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="requiredWord"/> is null or empty string.</exception>
        public void AddOptions(object options, string requiredWord)
        {
            Assumes.NotNull(options, "options");
            Assumes.NotNullOrEmpty(requiredWord, "requiredWord");

            AddOptions(options, requiredWord, MaximumDisplayWidth);
        }

        /// <summary>
        /// Adds a text block with options usage informations.
        /// </summary>
        /// <param name="options">The instance that collected command line arguments parsed with the <see cref="CommandLineParser"/> class.</param>
        /// <param name="requiredWord">The word to use when the option is required.</param>
        /// <param name="maximumLength">The maximum length of the help documentation.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="options"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when parameter <paramref name="requiredWord"/> is null or empty string.</exception>
        public void AddOptions(object options, string requiredWord, int maximumLength)
        {
            Assumes.NotNull(options, "options");
            Assumes.NotNullOrEmpty(requiredWord, "requiredWord");

            var optionList = ReflectionUtil.RetrievePropertyAttributeList<BaseOptionAttribute>(options);
            var optionHelp = ReflectionUtil.RetrieveMethodAttributeOnly<HelpOptionAttribute>(options);

            if (optionHelp != null)
            {
                optionList.Add(optionHelp);
            }

            if (optionList.Count == 0)
            {
                return;
            }

            int maxLength = GetMaxLength(optionList);
            _optionsHelp = new StringBuilder(_builderCapacity);
            int remainingSpace = maximumLength - (maxLength + 6);
            foreach (BaseOptionAttribute option in optionList)
            {
                AddOption(requiredWord, maxLength, option, remainingSpace);
            }
        }

        /// <summary>
        /// Builds a string that contains a parsing error message.
        /// </summary>
        /// <param name="options">
        /// An options target <see cref="CommandLineOptionsBase"/> instance that collected command line arguments parsed with the <see cref="CommandLineParser"/> class.
        /// </param>
        /// <returns>
        /// The <see cref="System.String"/> that contains the parsing error message.
        /// </returns>
        public string RenderParsingErrorsText(CommandLineOptionsBase options, int indent)
        {
            if (options.InternalLastPostParsingState.Errors.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder();
            foreach (var e in options.InternalLastPostParsingState.Errors)
            {
                var line = new StringBuilder();
                var isDefaultEnvironment = (e.ViolatingEnvironment == default(ParsingEnvironment));
                line.Append(StringUtil.Spaces(indent));
                if (!string.IsNullOrEmpty(e.BadOption.ShortName) && isDefaultEnvironment)
                {
                    line.Append('-');
                    line.Append(e.BadOption.ShortName);
                    if (!string.IsNullOrEmpty(e.BadOption.LongName)) line.Append('/');
                }
                if (!string.IsNullOrEmpty(e.BadOption.LongName))
                {
                    if (isDefaultEnvironment && !e.UnknownOption)
                        line.Append("--");
                    line.Append(e.BadOption.LongName);
                }
                line.Append(" ");
                if (e.ViolatesRequired)
                {
                    line.Append(_sentenceBuilder.RequiredOptionMissingText);
                }
                else
                {
                    line.Append(_sentenceBuilder.OptionWord);
                }
                if (e.ViolatesFormat)
                {
                    line.Append(" ");
                    line.Append(_sentenceBuilder.ViolatesFormatText);
                }
                if (e.ViolatesMutualExclusiveness)
                {
                    if (e.ViolatesFormat || e.ViolatesRequired)
                    {
                        line.Append(" ");
                        line.Append(_sentenceBuilder.AndWord);
                    }
                    line.Append(" ");
                    line.Append(_sentenceBuilder.ViolatesMutualExclusivenessText);
                }
                if (e.UnknownOption)
                {
                    line.Append(" ").Append(_sentenceBuilder.IsNotRecognized);
                }
                if (!isDefaultEnvironment)
                {
                    line.AppendFormat(" (in {0})", EnvToString(e.ViolatingEnvironment));
                }
                line.Append('.');
                text.AppendLine(line.ToString());
            }
            return text.ToString();
        }

        private void AddOption(string requiredWord, int maxLength, BaseOptionAttribute option, int widthOfHelpText)
        {
            _optionsHelp.Append("  ");
            StringBuilder optionName = new StringBuilder(maxLength);
            if (option.HasShortName)
            {
                if (_addDashesToOption)
                {
                    optionName.Append('-');
                }

                optionName.AppendFormat("{0}", option.ShortName);

                if (option.HasLongName)
                {
                    optionName.Append(", ");
                }
            }

            if (option.HasLongName)
            {
                if (_addDashesToOption)
                {
                    optionName.Append("--");
                }

                optionName.AppendFormat("{0}", option.LongName);
            }

            if (optionName.Length < maxLength)
            {
                _optionsHelp.Append(optionName.ToString().PadRight(maxLength));
            }
            else
            {
                _optionsHelp.Append(optionName.ToString());
            }

            _optionsHelp.Append("    ");
            if (option.Required)
            {
                option.HelpText = String.Format("{0} ", requiredWord) + option.HelpText;
            }

            FormatOptionHelpTextEventArgs e = new FormatOptionHelpTextEventArgs(option);
            OnFormatOptionHelpText(e);
            option.HelpText = e.Option.HelpText;

            if (!string.IsNullOrEmpty(option.HelpText))
            {
                do
                {
                    int wordBuffer = 0;
                    var words = option.HelpText.Split(new[] { ' ' });
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i].Length < (widthOfHelpText - wordBuffer))
                        {
                            _optionsHelp.Append(words[i]);
                            wordBuffer += words[i].Length;
                            if ((widthOfHelpText - wordBuffer) > 1 && i != words.Length - 1)
                            {
                                _optionsHelp.Append(" ");
                                wordBuffer++;
                            }
                        }
                        else if (words[i].Length >= widthOfHelpText && wordBuffer == 0)
                        {
                            _optionsHelp.Append(words[i].Substring(
                                0,
                                widthOfHelpText
                            ));
                            wordBuffer = widthOfHelpText;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                    option.HelpText = option.HelpText.Substring(Math.Min(
                        wordBuffer,
                        option.HelpText.Length
                    ))
                        .Trim();
                    if (option.HelpText.Length > 0)
                    {
                        _optionsHelp.Append(Environment.NewLine);
                        _optionsHelp.Append(new string(' ', maxLength + 6));
                    }
                } while (option.HelpText.Length > widthOfHelpText);
            }
            _optionsHelp.Append(option.HelpText);
            _optionsHelp.Append(Environment.NewLine);
            if (_additionalNewLineAfterOption)
                _optionsHelp.Append(Environment.NewLine);
        }

        /// <summary>
        /// Returns the help informations as a <see cref="System.String"/>.
        /// </summary>
        /// <returns>The <see cref="System.String"/> that contains the help informations.</returns>
        public override string ToString()
        {
            const int extraLength = 10;
            var builder = new StringBuilder(GetLength(_heading) + GetLength(_copyright) +
                GetLength(_preOptionsHelp) + GetLength(_optionsHelp) + extraLength
            );

            builder.Append(_heading);
            if (!string.IsNullOrEmpty(_copyright))
            {
                builder.Append(Environment.NewLine);
                builder.Append(_copyright);
            }
            if (_preOptionsHelp.Length > 0)
            {
                builder.Append(Environment.NewLine);
                builder.Append(_preOptionsHelp.ToString());
            }
            if (_optionsHelp != null && _optionsHelp.Length > 0)
            {
                builder.Append(Environment.NewLine);
                builder.Append(Environment.NewLine);
                builder.Append(_optionsHelp.ToString());
            }
            if (_postOptionsHelp.Length > 0)
            {
                builder.Append(Environment.NewLine);
                builder.Append(_postOptionsHelp.ToString());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts the help informations to a <see cref="System.String"/>.
        /// </summary>
        /// <param name="info">This <see cref="HelpText"/> instance.</param>
        /// <returns>The <see cref="System.String"/> that contains the help informations.</returns>
        public static implicit operator string(HelpText info)
        {
            return info.ToString();
        }

        private void AddLine(StringBuilder builder, string value)
        {
            Assumes.NotNull(value, "value");

            AddLine(builder, value, MaximumDisplayWidth);
        }

        private static void AddLine(StringBuilder builder, string value, int maximumLength)
        {
            Assumes.NotNull(value, "value");

            if (builder.Length > 0)
            {
                builder.Append(Environment.NewLine);
            }
            do
            {
                int wordBuffer = 0;
                string[] words = value.Split(new[] { ' ' });
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i].Length < (maximumLength - wordBuffer))
                    {
                        builder.Append(words[i]);
                        wordBuffer += words[i].Length;
                        if ((maximumLength - wordBuffer) > 1 && i != words.Length - 1)
                        {
                            builder.Append(" ");
                            wordBuffer++;
                        }
                    }
                    else if (words[i].Length >= maximumLength && wordBuffer == 0)
                    {
                        builder.Append(words[i].Substring(0, maximumLength));
                        wordBuffer = maximumLength;
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
                value = value.Substring(Math.Min(wordBuffer, value.Length));
                if (value.Length > 0)
                {
                    builder.Append(Environment.NewLine);
                }
            } while (value.Length > maximumLength);
            builder.Append(value);
        }

        private static int GetLength(string value)
        {
            if (value == null)
            {
                return 0;
            }
            return value.Length;
        }

        private static int GetLength(StringBuilder value)
        {
            if (value == null)
            {
                return 0;
            }
            return value.Length;
        }

        private static string EnvToString(ParsingEnvironment parsingEnvironment)
        {
            switch (parsingEnvironment)
            {
                case ParsingEnvironment.CommandLine:
                    return "command line args";
                case ParsingEnvironment.EnvironmentVariable:
                    return "environment variable";
                case ParsingEnvironment.AppConfig:
                    return "app.config appSettings";
                default:
                    return "(not-specified)";
            }
        }

        //ex static
        private int GetMaxLength(IList<BaseOptionAttribute> optionList)
        {
            int length = 0;
            foreach (BaseOptionAttribute option in optionList)
            {
                int optionLength = 0;
                bool hasShort = option.HasShortName;
                bool hasLong = option.HasLongName;
                if (hasShort)
                {
                    optionLength += option.ShortName.Length;
                    if (AddDashesToOption)
                        ++optionLength;
                }
                if (hasLong)
                {
                    optionLength += option.LongName.Length;
                    if (AddDashesToOption)
                        optionLength += 2;
                }
                if (hasShort && hasLong)
                {
                    optionLength += 2; // ", "
                }
                length = Math.Max(length, optionLength);
            }
            return length;
        }

        protected virtual void OnFormatOptionHelpText(FormatOptionHelpTextEventArgs e)
        {
            EventHandler<FormatOptionHelpTextEventArgs> handler = FormatOptionHelpText;

            if (handler != null)
                handler(this, e);
        }
    }
    #endregion
}