// MIT License
//
// Copyright (c) 2020 - 2026 Arsene Tochemey Gandote
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetCore8583.Tracer;
using Xunit;

namespace NetCore8583.Test.Tracer
{
    public class TestSimpleTraceGenerator
    {
        [Fact]
        public void InitialValueIsReturned()
        {
            var gen = new SimpleTraceGenerator(1);
            Assert.Equal(0, gen.LastTrace);
            Assert.Equal(1, gen.NextTrace());
        }

        [Fact]
        public void InitialValueMaxIsReturned()
        {
            var gen = new SimpleTraceGenerator(999999);
            Assert.Equal(999999, gen.NextTrace());
        }

        [Fact]
        public void SequentialValues()
        {
            var gen = new SimpleTraceGenerator(10);
            Assert.Equal(10, gen.NextTrace());
            Assert.Equal(11, gen.NextTrace());
            Assert.Equal(12, gen.NextTrace());
            Assert.Equal(12, gen.LastTrace);
        }

        [Fact]
        public void WrapsAroundAt999999()
        {
            var gen = new SimpleTraceGenerator(999999);
            Assert.Equal(999999, gen.NextTrace());
            Assert.Equal(1, gen.NextTrace());
            Assert.Equal(1, gen.LastTrace);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000000)]
        [InlineData(-1)]
        public void OutOfRangeInitialValueThrows(int value)
        {
            Assert.Throws<ArgumentException>(() => new SimpleTraceGenerator(value));
        }

        /// <summary>
        ///     Regression test for the "increment under lock, read outside lock" race: two
        ///     threads can each increment to a distinct value under the lock, then both read the
        ///     shared field afterwards and observe the same (higher) value, so one thread's
        ///     result is silently duplicated and the value it actually produced is silently
        ///     skipped. That window is a couple of IL instructions wide, so it needs real OS
        ///     threads released at the same instant (a <see cref="Barrier" />, not thread-pool
        ///     <see cref="Task.Run(Action)" /> scheduling, which staggers start times enough to
        ///     usually miss it) and a large number of calls hammering the lock back-to-back to
        ///     make hitting that window near-certain rather than a matter of luck.
        ///     Confirmed red against the pre-fix implementation (return read outside the lock)
        ///     and green against the fix (return the value captured inside the lock).
        /// </summary>
        [Fact]
        public void ThreadSafetyProducesUniqueSequentialValues()
        {
            const int threadCount = 32;
            const int callsPerThread = 5000;
            const int totalCalls = threadCount * callsPerThread;

            var gen = new SimpleTraceGenerator(1);
            var results = new ConcurrentBag<int>();
            var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (var t = 0; t < threadCount; t++)
                threads[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < callsPerThread; i++)
                        results.Add(gen.NextTrace());
                }) { IsBackground = true };

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            var expected = new HashSet<int>(Enumerable.Range(1, totalCalls));
            var actual = new HashSet<int>(results);

            Assert.Equal(totalCalls, results.Count);
            Assert.Equal(expected, actual);
        }
    }
}
