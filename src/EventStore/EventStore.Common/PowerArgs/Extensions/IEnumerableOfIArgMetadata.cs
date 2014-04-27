using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerArgs
{
    /// <summary>
    /// Extension methods that make it easy to work with metadata collections
    /// </summary>
    public static class IArgMetadataEx
    {
        internal static List<T> AssertAreAllInstanceOf<T>(this IEnumerable<IArgMetadata> all)
        {
            var valid = from meta in all where meta is T == true select (T)meta;
            var invalid = from meta in all where meta is T == false select meta;

            foreach (var invalidMetadata in invalid)
            {
                throw new InvalidArgDefinitionException("Metadata of type '" + invalidMetadata.GetType().Name + "' does not implement " + typeof(ICommandLineArgumentsDefinitionMetadata).Name);
            }

            return valid.ToList();
        }

        /// <summary>
        /// Returns true if the given collection of metadata contains metadata of the generic type T
        /// provided.
        /// </summary>
        /// <typeparam name="T">The type of metadata to search for</typeparam>
        /// <param name="metadata">The list of metadata to search</param>
        /// <returns>rue if the given collection of metadata contains metadata of the generic type T
        /// provided, otherwise false</returns>
        public static bool HasMeta<T>(this IEnumerable<IArgMetadata> metadata) where T : class
        {
            return Metas<T>(metadata).Count > 0;
        }

        /// <summary>
        /// Gets the first instance of metadata of the given generic type T in the collection
        /// or null if it was not found.
        /// </summary>
        /// <typeparam name="T">The type of metadata to search for</typeparam>
        /// <param name="metadata">The list of metadata to search</param>
        /// <returns>the first instance of an metadata of the given generic type T in the collection
        /// or null if it was not found</returns>
        public static T Meta<T>(this IEnumerable<IArgMetadata> metadata) where T : class
        {
            return Metas<T>(metadata).FirstOrDefault();
        }

        /// <summary>
        /// Try to get the first instance of metadata of the given generic type T in the collection.
        /// </summary>
        /// <typeparam name="T">The type of metadata to search for</typeparam>
        /// <param name="metadata">The list of metadata to search</param>
        /// <param name="ret">the our variable to set if the metadata was found</param>
        /// <returns>true if the metadata was found, otherwise false</returns>
        public static bool TryGetMeta<T>(this IEnumerable<IArgMetadata> metadata, out T ret) where T : class
        {
            if (metadata.HasMeta<T>())
            {
                ret = metadata.Meta<T>();
                return true;
            }
            else
            {
                ret = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the subset of metadata of the given generic type T from the collection.
        /// </summary>
        /// <typeparam name="T">The type of metadata to search for</typeparam>
        /// <param name="metadata">The list of metadata to search</param>
        /// <returns>the subset of metadata of the given generic type T from the collection</returns>
        public static List<T> Metas<T>(this IEnumerable<IArgMetadata> metadata) where T : class
        {
            var match = from a in metadata where a is T select (T)a;
            return match.ToList();
        }
    }
}
