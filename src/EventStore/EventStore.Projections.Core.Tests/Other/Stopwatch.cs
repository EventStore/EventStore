using System;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Other
{
    [TestFixture]
    class Stopwatch
    {
        [Test]
        public void MeasureStopwatch()
        {
            var sw = new System.Diagnostics.Stopwatch();
            var measured = new System.Diagnostics.Stopwatch();
            sw.Reset();
            sw.Start();
            measured.Start();
            measured.Stop();
            var warmupTemp = measured.ElapsedMilliseconds;
            sw.Stop();
            var warmupTime = sw.ElapsedMilliseconds;
            measured.Reset();
            sw.Reset();

            sw.Start();
            sw.Stop();
            var originalTime = sw.ElapsedMilliseconds;
            sw.Reset();

            sw.Start();
            for (var i = 0; i < 1000000; i++)
            {
                measured.Start();
                measured.Stop();
                var temp = measured.ElapsedMilliseconds;
            }
            sw.Stop();
            var measuredTime = sw.ElapsedMilliseconds;
            Console.WriteLine(measuredTime - originalTime);
        }
    }
}
