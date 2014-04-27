using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace PowerArgs
{
    /// <summary>
    /// A useful arg hook that will store the last used value for an argument and repeat it the next time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class StickyArg : ArgHook, ICommandLineArgumentMetadata
    {
        private static Lazy<IStickyArgPersistenceProvider> defaultPersistenceProvider = new Lazy<IStickyArgPersistenceProvider>(() => { return new DefaultStickyArgPersistenceProvider(); });

        private string file;
        private Dictionary<string, string> stickyArgs;
        private IStickyArgPersistenceProvider userSpecifiedPersistenceProvider;

        /// <summary>
        /// Marks a property as a sticky arg.  Use the default location to store sticky arguments (AppData/Roaming/PowerArgs/EXE_NAME.txt)
        /// </summary>
        public StickyArg()
        {
            Init(null);
        }

        /// <summary>
        /// Marks a property as a sticky arg.  Use the provided location to store sticky arguments (AppData/Roaming/PowerArgs/EXE_NAME.txt)
        /// </summary>
        public StickyArg(string file)
        {
            Init(file);
        }

        private void Init(string file)
        {
            BeforePopulatePropertyPriority = 10;
            stickyArgs = new Dictionary<string, string>();
            this.file = file;
        }

        /// <summary>
        /// If the user didn't specify a value on the command line then the StickyArg will try to load the last used
        /// value.
        /// </summary>
        /// <param name="Context">Used to see if the property was specified.</param>
        public override void BeforePopulateProperty(HookContext Context)
        {
            if (Context.ArgumentValue == null)
            {
                if (userSpecifiedPersistenceProvider == null && Context.Definition.Metadata.HasMeta<StickyArgPersistence>())
                {
                    userSpecifiedPersistenceProvider = Context.Definition.Metadata.Meta<StickyArgPersistence>().PersistenceProvider;
                }

                Context.ArgumentValue = GetStickyArg(Context.CurrentArgument.DefaultAlias);
            }
        }

        /// <summary>
        /// If the given property has a non null value then that value is persisted for the next use.
        /// </summary>
        /// <param name="Context">Used to see if the property was specified.</param>
        public override void AfterPopulateProperty(HookContext Context)
        {
            if (Context.ArgumentValue != null)
            {
                if (userSpecifiedPersistenceProvider == null && Context.Definition.Metadata.HasMeta<StickyArgPersistence>())
                {
                    userSpecifiedPersistenceProvider = Context.Definition.Metadata.Meta<StickyArgPersistence>().PersistenceProvider;
                }

                SetStickyArg(Context.CurrentArgument.DefaultAlias, Context.ArgumentValue);
            }
        }

        private string GetStickyArg(string name)
        {
            Load();
            string ret = null;
            if (stickyArgs.TryGetValue(name, out ret) == false) return null;
            return ret;
        }

        private void SetStickyArg(string name, string value)
        {
            Load();
            if (stickyArgs.ContainsKey(name))
            {
                stickyArgs[name] = value;
            }
            else
            {
                stickyArgs.Add(name, value);
            }
            Save();
        }

        private void Load()
        {
            var provider = userSpecifiedPersistenceProvider ?? defaultPersistenceProvider.Value;
            stickyArgs = provider.Load(file);
        }

        private void Save()
        {
            var provider = userSpecifiedPersistenceProvider ?? defaultPersistenceProvider.Value;
            provider.Save(stickyArgs, file);
        }
    }

    /// <summary>
    /// An interface used to implement custom saving and loading of persistent (sticky) args.
    /// </summary>
    public interface IStickyArgPersistenceProvider
    {
        /// <summary>
        /// This method is called when it is time to save the sticky args.
        /// </summary>
        /// <param name="stickyArgs">The names and values of the arguments to save.</param>
        /// <param name="pathInfo">The string that was passed to the StickyArg attribue (usually a file path).</param>
        void Save(Dictionary<string, string> stickyArgs, string pathInfo);
        /// <summary>
        /// This method is called when it is time to load the sticky args.
        /// </summary>
        /// <param name="pathInfo">The string that was passed to the StickyArg attribue (usually a file path).</param>
        /// <returns>The loaded sticky args.</returns>
        Dictionary<string, string> Load(string pathInfo);
    }

    /// <summary>
    /// An attribute you can put on a type in order to override how StickyArg properties are saved and loaded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class StickyArgPersistence : Attribute, ICommandLineArgumentsDefinitionMetadata
    {
        private Type persistenceProviderType;
        private IStickyArgPersistenceProvider _persistenceProvider;

        /// <summary>
        /// Gets the provider that will be used to save and load sticky args.
        /// </summary>
        public IStickyArgPersistenceProvider PersistenceProvider
        {
            get
            {
                if (_persistenceProvider != null) return _persistenceProvider;

                if (persistenceProviderType.GetInterfaces().Contains(typeof(IStickyArgPersistenceProvider)) == false)
                {
                    throw new InvalidArgDefinitionException("The given type does not implement '" + typeof(IStickyArgPersistenceProvider).Name + "'");
                }

                _persistenceProvider = (IStickyArgPersistenceProvider)Activator.CreateInstance(persistenceProviderType);
                return _persistenceProvider;
            }
        }

        /// <summary>
        /// Creates a new StickyArgPersistence attribute given the type of the persistence provider.
        /// </summary>
        /// <param name="persistenceProviderType">The type that implements IStickyArgPersistenceProvider and defines a default constructor.</param>
        public StickyArgPersistence(Type persistenceProviderType)
        {
            this.persistenceProviderType = persistenceProviderType;
        }
    }

    internal class DefaultStickyArgPersistenceProvider : IStickyArgPersistenceProvider
    {
        public void Save(Dictionary<string, string> stickyArgs, string pathInfo)
        {
            pathInfo = pathInfo ?? DefaultFilePath;

            if (Directory.Exists(Path.GetDirectoryName(pathInfo)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pathInfo));
            }

            var lines = (from k in stickyArgs.Keys select k + "=" + stickyArgs[k]).ToArray();
            File.WriteAllLines(pathInfo, lines);
        }

        public Dictionary<string, string> Load(string pathInfo)
        {
            pathInfo = pathInfo ?? DefaultFilePath;

            if (Directory.Exists(Path.GetDirectoryName(pathInfo)) == false)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pathInfo));
            }

            Dictionary<string, string> ret = new Dictionary<string, string>();
            if (File.Exists(pathInfo) == false) return ret;

            foreach (var line in File.ReadAllLines(pathInfo))
            {
                int separator = line.IndexOf("=");
                if (separator < 0 || line.Trim().StartsWith("#")) continue;

                string key = line.Substring(0, separator).Trim();
                string val = separator == line.Length - 1 ? "" : line.Substring(separator + 1).Trim();

                ret.Add(key, val);
            }

            return ret;
        }

        private string DefaultFilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                   "PowerArgs",
                   Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)) + ".StickyArgs.txt";
            }
        }
    }
}
