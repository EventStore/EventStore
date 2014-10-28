using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace EventStore.Rags
{
    /// <summary>
    /// A class that lets you customize how your usage displays
    /// </summary>
    public class ArgUsageOptions
    {
        /// <summary>
        /// Set to true if you want to show the type column (true by default)
        /// </summary>
        public bool ShowType { get; set; }

        /// <summary>
        /// Set to true to list possible values (usually for enums, true by default)
        /// </summary>
        public bool ShowPossibleValues { get; set; }

        /// <summary>
        /// Set to true if you want to show default values after the description (true by default)
        /// </summary>
        public bool AppendDefaultValueToDescription { get; set; }

        /// <summary>
        /// Set this to ensure the usage generator only shows usage info for the specified action.  You will typically
        /// populate this by looking at the ArgException that you're probably catching.
        /// </summary>

        /// <summary>
        /// Creates a new instance of ArgUsageOptions
        /// </summary>
        public ArgUsageOptions()
        {
            ShowType = false;
            ShowPossibleValues = true;
            AppendDefaultValueToDescription = true;
        }
    }

    /// <summary>
    /// An attribute used to hook into the usage generation process and influence
    /// the content that is written.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class UsageHook : Attribute
    {
        /// <summary>
        /// An event you can subscribe to in the case where you created
        /// your hook in running code rather than as a declarative attribute.
        /// </summary>
        public event Action<ArgumentUsageInfo> HookExecuting;

        /// <summary>
        /// This hook gets called when the property it is attached to is having
        /// its usage generated.  You can override this method and manipulate the
        /// properties of the given usage info object.
        /// </summary>
        /// <param name="info">An object that you can use to manipulate the usage output.</param>
        public virtual void BeforeGenerateUsage(ArgumentUsageInfo info)
        {
            if (HookExecuting != null) HookExecuting(info);
        }
    }

    /// <summary>
    /// A class that represents usage info to be written to the console.
    /// </summary>
    public class ArgumentUsageInfo
    {
        private static Dictionary<string, string> KnownTypeMappings = new Dictionary<string, string>()
        {
            {"Int32", "integer"},
            {"Int64", "integer"},
            {"Boolean", "switch"},
            {"Guid", "guid"},
        };

        /// <summary>
        /// The name that will be written as part of the usage.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Aliases for this argument that will be honored by the parser.  This
        /// includes shortcuts and long form aliases, but can be extended further.
        /// </summary>
        public List<string> Aliases { get; private set; }

        /// <summary>
        /// Possible values for this option.  This is auto populated for enums and includes the description if specified.
        /// </summary>
        public List<string> PossibleValues { get; private set; }

        /// <summary>
        /// Indicates that the argument is required.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// The friendly type name that will be displayed to the user.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The expected position of the argument, or null if not a positioning is not supported for the given argument.
        /// </summary>
        public int? Position { get; set; }

        /// <summary>
        /// The description that will be written as part of the usage.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The grouping that will be used to group the usage.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// If set to true, the argument usage will not be written.
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// True if this is the "Action" property
        /// </summary>
        public bool IsAction { get; set; }

        /// <summary>
        /// True if this represents a nested action argument property
        /// </summary>
        [Obsolete("Usage is not affected by this property")]
        public bool IsActionArgs { get; set; }

        /// <summary>
        /// The reflected property that this info object represents
        /// </summary>
        public PropertyInfo Property { get; set; }

        /// <summary>
        /// The default value for the argument
        /// </summary>
        public object DefaultValue { get; set; }

        private ArgumentUsageInfo()
        {
            Aliases = new List<string>();
            PossibleValues = new List<string>();
        }

        /// <summary>
        /// Generate a new info instance given a reflected property. 
        /// </summary>
        /// <param name="toAutoGen">The property to use to seed the usage info</param>
        public ArgumentUsageInfo(PropertyInfo toAutoGen)
            : this()
        {
            Property = toAutoGen;
            Ignore = false;
            IsAction = false;

            Name = "-" + NameTranslators.CombineByPascalCase(toAutoGen.Name, "-").ToLower();

            Aliases.Add("-" + Property.Name);
            if (Aliases.Count == 0)
            {
                //add shortcut alias
                Aliases.Add("-" + Name.ToLower());
            }
            Type = toAutoGen.PropertyType.Name;
            if (KnownTypeMappings.ContainsKey(Type))
            {
                Type = KnownTypeMappings[Type];
            }
            else
            {
                Type = Type.ToLower();
            }

            Position = 0; 
            Description = Property.HasAttr<ArgDescriptionAttribute>() ? Property.Attr<ArgDescriptionAttribute>().Description : "";
            Group = Property.HasAttr<ArgDescriptionAttribute>() ? Property.Attr<ArgDescriptionAttribute>().Group : "";

            if (Property.PropertyType.IsEnum)
            {
                foreach (var val in toAutoGen.PropertyType.GetFields().Where(v => v.IsSpecialName == false))
                {
                    var description = val.HasAttr<ArgDescriptionAttribute>() ? " - " + val.Attr<ArgDescriptionAttribute>().Description : "";
                    var valText = val.Name;
                    PossibleValues.Add(valText + description);
                }
            }
        }
    }

    public static class ArgUsage
    {
        internal static Dictionary<PropertyInfo, List<UsageHook>> ExplicitPropertyHooks = new Dictionary<PropertyInfo, List<UsageHook>>();
        internal static List<UsageHook> GlobalUsageHooks = new List<UsageHook>();

        // TODO P0 - Need unit tests for usage hooks

        /// <summary>
        /// Generates usage documentation for the given argument scaffold type.
        /// </summary>
        /// <typeparam name="T">Your custom argument scaffold type</typeparam>
        /// <param name="exeName">The name of your program or null if you want PowerArgs to automatically detect it.</param>
        /// <param name="options">Specify custom usage options</param>
        /// <returns>the usage documentation as a string</returns>
        public static string GetUsage<T>(string exeName = null)
        {
            return GetStyledUsage<T>(exeName).ToString();
        }

        /// <summary>
        /// Generates color styled usage documentation for the given arguments definition.  
        /// </summary>
        /// <param name="definition">The definition of the command line arguments for a program</param>
        /// <param name="exeName">The name of your program or null if you want PowerArgs to automatically detect it.</param>
        /// <param name="options">Specify custom usage options</param>
        /// <returns>the usage documentation as a styled string that can be printed to the console</returns>
        public static ConsoleString GetStyledUsage<T>(string exeName = null)
        {
            var options = new ArgUsageOptions();
            if (exeName == null)
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly == null)
                {
                    throw new InvalidOperationException("PowerArgs could not determine the name of your executable automatically.  This may happen if you run GetUsage<T>() from within unit tests.  Use GetUsageT>(string exeName) in unit tests to avoid this exception.");
                }
                exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            }

            ConsoleString ret = new ConsoleString();

            ret += new ConsoleString("Usage: " + exeName, ConsoleColor.Cyan);


            ret.AppendUsingCurrentFormat(" options\n\n");

            ret += GetOptionsUsage(typeof(T).GetProperties(), false, options);

            ret += "\n";

            return ret;
        }

        private static ConsoleString GetOptionsUsage(IEnumerable<PropertyInfo> opts, bool ignoreActionProperties, ArgUsageOptions options)
        {
            if (opts.Count() == 0)
            {
                return new ConsoleString("There are no options");
            }

            var usageInfos = opts.Select(o => new ArgumentUsageInfo(o));

            var hasPositionalArgs = usageInfos.Where(i => i.Position >= 0).Count() > 0;

            List<ConsoleString> columnHeaders = new List<ConsoleString>()
            {
                new ConsoleString("OPTION", ConsoleColor.Yellow),
                new ConsoleString("DESCRIPTION", ConsoleColor.Yellow),
            };

            bool hasTypeCol = false, hasPosCol = false;

            int insertPosition = 1;
            if (options.ShowType)
            {
                columnHeaders.Insert(insertPosition++, new ConsoleString("TYPE", ConsoleColor.Yellow));
                hasTypeCol = true;
            }

            List<List<ConsoleString>> rows = new List<List<ConsoleString>>();
            string currentGroup = String.Empty;
            foreach (ArgumentUsageInfo usageInfo in usageInfos.OrderBy(x => x.Group).OrderBy(i => i.Position >= 0 ? i.Position : 1000))
            {
                if (currentGroup != usageInfo.Group && !String.IsNullOrEmpty(usageInfo.Group))
                {
                    currentGroup = usageInfo.Group;
                    rows.Add(new List<ConsoleString>() { ConsoleString.Empty, ConsoleString.Empty, ConsoleString.Empty });
                    rows.Add(new List<ConsoleString>()
                    {
                        new ConsoleString(currentGroup),
                        ConsoleString.Empty,
                        ConsoleString.Empty
                    });
                }

                var hooks = new List<UsageHook>();
                if (usageInfo.Property != null && ArgUsage.ExplicitPropertyHooks.ContainsKey(usageInfo.Property))
                {
                    hooks.AddRange(ArgUsage.ExplicitPropertyHooks[usageInfo.Property]);
                }

                hooks.AddRange(ArgUsage.GlobalUsageHooks);

                foreach (var hook in hooks)
                {
                    hook.BeforeGenerateUsage(usageInfo);
                }


                if (usageInfo.Ignore) continue;
                if (usageInfo.IsAction && ignoreActionProperties) continue;

                var positionString = new ConsoleString(usageInfo.Position >= 0 ? usageInfo.Position + "" : "NA");
                var requiredString = new ConsoleString(usageInfo.IsRequired ? "*" : "", ConsoleColor.Red);
                var descriptionString = new ConsoleString(usageInfo.Description);
                if (options.AppendDefaultValueToDescription && usageInfo.DefaultValue != null) descriptionString += new ConsoleString(" [default=" + usageInfo.DefaultValue.ToString() + "]", ConsoleColor.DarkGreen);

                var typeString = new ConsoleString(usageInfo.Type);

                var aliases = usageInfo.Aliases.OrderBy(a => a.Length).ToList();
                var maxInlineAliasLength = 8;
                string inlineAliasInfo = "";

                int aliasIndex;
                for (aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
                {
                    var proposedInlineAliases = inlineAliasInfo == string.Empty ? aliases[aliasIndex] : inlineAliasInfo + ", " + aliases[aliasIndex];
                    if (proposedInlineAliases.Length <= maxInlineAliasLength)
                    {
                        inlineAliasInfo = proposedInlineAliases;
                    }
                    else
                    {
                        break;
                    }
                }

                if (inlineAliasInfo != string.Empty) inlineAliasInfo = " (" + inlineAliasInfo + ")";

                rows.Add(new List<ConsoleString>()
                {
                    new ConsoleString("")+("-" + usageInfo.Name + inlineAliasInfo),
                    descriptionString,
                });

                insertPosition = 1;
                if (options.ShowType)
                {
                    rows.Last().Insert(insertPosition++, typeString + requiredString);
                }

                for (int i = aliasIndex; i < aliases.Count; i++)
                {
                    rows.Add(new List<ConsoleString>()
                    {
                        new ConsoleString("  "+aliases[i]),
                        ConsoleString.Empty,
                    });

                    if (hasTypeCol) rows.Last().Add(ConsoleString.Empty);
                    if (hasPosCol) rows.Last().Add(ConsoleString.Empty);
                }

                if (options.ShowPossibleValues)
                {
                    foreach (var possibleValue in usageInfo.PossibleValues)
                    {
                        rows.Add(new List<ConsoleString>()
                    {
                        ConsoleString.Empty,
                        new ConsoleString("  "+possibleValue),
                    });

                        if (hasTypeCol) rows.Last().Insert(rows.Last().Count - 1, ConsoleString.Empty);
                        if (hasPosCol) rows.Last().Insert(rows.Last().Count - 1, ConsoleString.Empty);
                    }
                }
            }

            return FormatAsTable(columnHeaders, rows, "   ");
        }

        private static ConsoleString FormatAsTable(List<ConsoleString> columns, List<List<ConsoleString>> rows, string rowPrefix = "")
        {
            if (rows.Count == 0) return new ConsoleString();

            Dictionary<int, int> maximums = new Dictionary<int, int>();

#if __MonoCS__
            int optionDescriptionWidth = 20;
            int standardColumnWidth = 80;

            List<int> columnWidths = new List<int>();
#endif

            for (int i = 0; i < columns.Count; i++)
            {
#if __MonoCS__
                columnWidths.Add(i == 0 ? optionDescriptionWidth : standardColumnWidth - optionDescriptionWidth);
#endif
                maximums.Add(i, columns[i].Length);
            }
            for (int i = 0; i < columns.Count; i++)
            {
                foreach (var row in rows)
                {
#if __MonoCS__
                       maximums[i] = columnWidths[i];
#else
                    maximums[i] = Math.Max(maximums[i], row[i].Length);
#endif
                }
            }

            ConsoleString ret = new ConsoleString();
            int buffer = 3;

            ret += rowPrefix;
            for (int i = 0; i < columns.Count; i++)
            {
                var val = columns[i];
                while (val.Length < maximums[i] + buffer) val += " ";
                ret += val;
            }

            ret += "\n";

            foreach (var row in rows)
            {
                ret += rowPrefix;
                for (int i = 0; i < columns.Count; i++)
                {
                    var val = row[i];
                    while (val.Length < maximums[i] + buffer) val += " ";

                    ret += val;
                }
                ret += "\n";
            }

            return ret;
        }
    }
}