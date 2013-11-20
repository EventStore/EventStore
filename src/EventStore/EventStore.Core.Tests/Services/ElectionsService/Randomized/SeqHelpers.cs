using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
    public static class SeqHelpers
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Func<int, int> rndNext)
        {
            var array = source.ToArray();
            var n = array.Length;
            while (n > 1)
            {
                var k = rndNext(n);
                n--;
                var temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
            return array;
        }
    }
}