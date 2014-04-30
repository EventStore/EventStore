using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowerArgs
{
    /// <summary>
    /// This class tracks the command line aliases for a CommandLineArgument and a CommandLineAction.
    /// It combines the aliases that have been retrieved from the ArgShortcut attibute and any additional
    /// aliases that may have been added to the model manually into a single collection.  It also makes sure that those two sources
    /// of aliases don't conflict.
    /// 
    /// </summary>
    public class AliasCollection : IList<string>
    {
        private class AliasCollectionEnumerator : IEnumerator<string>
        {
            AliasCollection c;

            IEnumerator<string> wrapped;

            public AliasCollectionEnumerator(AliasCollection c)
            {
                this.c = c;
                List<string> completeList = new List<string>();
                completeList.AddRange(c.overrides);
                completeList.AddRange(c.metadataEval());
                wrapped = completeList.GetEnumerator();
            }

            public string Current
            {
                get { return wrapped.Current; }
            }

            public void Dispose()
            {
                wrapped.Dispose();
            }

            object System.Collections.IEnumerator.Current
            {
                get { return wrapped.Current; }
            }

            public bool MoveNext()
            {
                return wrapped.MoveNext();
            }

            public void Reset()
            {
                wrapped.Reset();
            }
        }

        List<string> overrides;

        Func<List<string>> metadataEval;
        Func<bool> ignoreCaseEval;

        private IList<string> NormalizedList
        {
            get
            {
                List<string> ret = new List<string>();
                foreach (var item in this) ret.Add(item);
                return ret.AsReadOnly();
            }
        }

        private AliasCollection(Func<List<string>> metadataEval, Func<bool> ignoreCaseEval)
        {
            this.metadataEval = metadataEval;
            overrides = new List<string>();
            this.ignoreCaseEval = ignoreCaseEval;
        }

        internal AliasCollection(Func<List<ArgShortcut>> aliases, Func<bool> ignoreCaseEval, bool stripLeadingArgInticatorsOnAttributeValues = true) : this(EvaluateAttributes(aliases, stripLeadingArgInticatorsOnAttributeValues), ignoreCaseEval) { }


        private static Func<List<string>> EvaluateAttributes(Func<List<ArgShortcut>> eval, bool stripLeadingArgInticatorsOnAttributeValues)
        {
            return () =>
            {
                List<ArgShortcut> shortcuts = eval();

                List<string> ret = new List<string>();

                foreach (var attr in shortcuts.OrderBy(a => a.Shortcut == null ? 0 : a.Shortcut.Length))
                {
                    bool noShortcut = false;
                    if (attr.Policy == ArgShortcutPolicy.NoShortcut)
                    {
                        noShortcut = true;
                    }

                    foreach (var shortcut in attr.Shortcut.Split('|'))
                    {
                        var value = shortcut;
                        if (noShortcut && shortcut != null)
                        {
                            throw new InvalidArgDefinitionException("You cannot specify a shortcut value and an ArgShortcutPolicy of NoShortcut");
                        }

                        if (shortcut != null)
                        {
                            if (stripLeadingArgInticatorsOnAttributeValues == true)
                            {
                                if (shortcut.StartsWith("-")) value = value.Substring(1);
                                else if (shortcut.StartsWith("/")) value = value.Substring(1);
                            }
                        }

                        if (shortcut != null)
                        {
                            if (ret.Contains(shortcut))
                            {
                                throw new InvalidArgDefinitionException("Duplicate ArgShortcut attributes with value: " + shortcut);
                            }
                            ret.Add(shortcut);
                        }
                    }
                }

                return ret;
            };
        }

        /// <summary>
        /// Gets the index of the given alias in the collection.
        /// </summary>
        /// <param name="item">the alias to look for</param>
        /// <returns>The index of item if found in the list; otherwise, -1.</returns>
        public int IndexOf(string item)
        {
            return NormalizedList.IndexOf(item);
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="index">Not supported</param>
        /// <param name="item">Not supported</param>
        public void Insert(int index, string item)
        {
            throw new NotSupportedException("Insert is not supported");
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="index">Not supported</param>
        public void RemoveAt(int index)
        {
            throw new NotSupportedException("RemoveAt is not supported");
        }

        /// <summary>
        /// The setter is not supported.  The getter returns the item at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>the item at the specified index</returns>
        public string this[int index]
        {
            get
            {
                return NormalizedList[index];
            }
            set
            {
                throw new NotSupportedException("Setting by index is not supported");
            }
        }

        /// <summary>
        /// Adds the given aliases to the collection. 
        /// </summary>
        /// <param name="items">The alias to add</param>
        public void AddRange(IEnumerable<string> items)
        {
            foreach (var item in items) Add(item);
        }

        /// <summary>
        /// Adds the given alias to the collection.  An InvalidArgDefinitionException is thrown if you try to add
        /// the same alias twice (case sensitivity is determined by the CommandLineArgument or CommandLineAction).
        /// </summary>
        /// <param name="item">The alias to add</param>
        public void Add(string item)
        {
            if (NormalizedList.Contains(item, new CaseAwareStringComparer(ignoreCaseEval())))
            {
                throw new InvalidArgDefinitionException("The alias '" + item + "' has already been added");
            }

            overrides.Add(item);
        }

        /// <summary>
        /// Clear is not supported, use ClearOverrides() to clear items that have manually been added
        /// </summary>
        public void Clear()
        {
            throw new NotSupportedException("Clear is not supported, use ClearOverrides() to clear items that have manually been added");
        }

        /// <summary>
        /// Clears the aliases that have been manually addd to this collection via Add() or AddRange().
        /// Aliases that are inferred from the Metadata will still be present in the collection. 
        /// </summary>
        public void ClearOverrides()
        {
            overrides.Clear();
        }

        /// <summary>
        /// Tests to see if this Alias collection contains the given item.  Case sensitivity is enforced
        /// based on the CommandLineArgument or CommandLineAction.
        /// </summary>
        /// <param name="item">The item to test for containment</param>
        /// <returns>True if the collection contains the item, otherwise false</returns>
        public bool Contains(string item)
        {
            return NormalizedList.Contains(item, new CaseAwareStringComparer(ignoreCaseEval()));
        }

        /// <summary>
        /// Copies this collection to an array, starting at the given index
        /// </summary>
        /// <param name="array">the destination array</param>
        /// <param name="arrayIndex">the starting index of where to place the elements into the destination</param>
        public void CopyTo(string[] array, int arrayIndex)
        {
            NormalizedList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the count of aliases
        /// </summary>
        public int Count
        {
            get { return NormalizedList.Count(); }
        }

        /// <summary>
        /// Not read only ever
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes the given alias from the collection if it was added via Add() or AddRange().  If
        /// it was added by injecting metadata into a CommandLineArgument or a CommandLineAction then
        /// an InvalidOperationException will be thrown.  The correct way to remove metadata injected
        /// aliases is to remove it from the metadata directly.
        /// </summary>
        /// <param name="item">the item to remove</param>
        /// <returns>true if the alias was removed, false otherwise</returns>
        public bool Remove(string item)
        {
            if (overrides.Contains(item))
            {
                return overrides.Remove(item);
            }
            else if (metadataEval().Contains(item))
            {
                throw new InvalidOperationException("The alias '" + item + "' was added via metadata and cannot be removed from this collection");
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets an enumerator capable of enumerating all aliases
        /// </summary>
        /// <returns>an enumerator capable of enumerating all aliases</returns>
        public IEnumerator<string> GetEnumerator()
        {
            return new AliasCollectionEnumerator(this);
        }

        /// <summary>
        /// Gets an enumerator capable of enumerating all aliases
        /// </summary>
        /// <returns>an enumerator capable of enumerating all aliases</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new AliasCollectionEnumerator(this);
        }
    }
}
