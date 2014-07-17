using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Common.Utils
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Safe<T>(this IEnumerable<T> collection)
        {
            return collection ?? Enumerable.Empty<T>();
        }

        public static bool Contains<T>(this IEnumerable<T> collection, Predicate<T> condition)
        {
            return collection.Any(x => condition(x));
        }

        public static bool IsEmpty<T>(this IEnumerable<T> collection)
        {
            if (collection == null)
                return true;
            var coll = collection as ICollection;
            if (coll != null)
                return coll.Count == 0;
            return !collection.Any();
        }

        public static bool IsNotEmpty<T>(this IEnumerable<T> collection)
        {
            return !IsEmpty(collection);
        }

                public static IEnumerable<T> Distinct<T, TComparator>(this IEnumerable<T> self, Func<T, TComparator> compareBy)
        {
            return self.Distinct(new EqualityComparer<T, TComparator>(compareBy));
        }

        private class EqualityComparer<T, TComparator> : IEqualityComparer<T> 
        {
            private readonly Func<T, TComparator> _compareBy;

            public EqualityComparer(Func<T, TComparator> compareBy)
            {
                _compareBy = compareBy;
            }

            public bool Equals(T x, T y)
            {
                return _compareBy(x).Equals(_compareBy(y));
            }

            public int GetHashCode(T obj)
            {
                return _compareBy(obj).GetHashCode();
            }
        }

        public static IEnumerable<T> MergeOrdered<T, TOrder>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, TOrder> orderFunc) 
            where TOrder : IComparable<TOrder>
        {
            using (var firstEnumerator = first.GetEnumerator())
            using (var secondEnumerator = second.GetEnumerator())
            {

                var elementsLeftInFirst = firstEnumerator.MoveNext();
                var elementsLeftInSecond = secondEnumerator.MoveNext();
                while (elementsLeftInFirst || elementsLeftInSecond)
                {
                    if (!elementsLeftInFirst)
                    {
                        do
                        {
                            yield return secondEnumerator.Current;
                        } while (secondEnumerator.MoveNext());
                        yield break;
                    }

                    if (!elementsLeftInSecond)
                    {
                        do
                        {
                            yield return firstEnumerator.Current;
                        } while (firstEnumerator.MoveNext());
                        yield break;
                    }

                    if (orderFunc(firstEnumerator.Current).CompareTo(orderFunc(secondEnumerator.Current)) < 0)
                    {
                        yield return firstEnumerator.Current;
                        elementsLeftInFirst = firstEnumerator.MoveNext();
                    }
                    else
                    {
                        yield return secondEnumerator.Current;
                        elementsLeftInSecond = secondEnumerator.MoveNext();
                    }
                }
            }
        }
    }
}
