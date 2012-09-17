// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using EventStore.Core.DataStructures;
using NUnit.Framework;

namespace EventStore.Core.Tests.DataStructures
{
    [TestFixture]
    public class pairing_heap_should
    {
        PairingHeap<int> _heap;

        [SetUp]
        public void SetUp()
        {
            _heap = new PairingHeap<int>();
        }

        [TearDown]
        public void TearDown()
        {
            _heap = null;
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_comparer()
        {
            new PairingHeap<int>(null as IComparer<int>);
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void throw_argumentnullexception_when_given_null_compare_func()
        {
            new PairingHeap<int>(null as Func<int, int, bool>);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void throw_invalidoperationexception_when_trying_to_find_min_element_on_empty_queue()
        {
            var x = _heap.FindMin();
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void throw_invalidoperationexception_when_trying_to_delete_min_element_on_empty_queue()
        {
            var x = _heap.FindMin();
        }

        [Test]
        public void return_correct_min_element_and_keep_it_in_heap_on_findmin_operation()
        {
            _heap.Add(9);
            _heap.Add(7);
            _heap.Add(5);
            _heap.Add(3);

            Assert.That(_heap.FindMin(), Is.EqualTo(3));
            Assert.That(_heap.Count, Is.EqualTo(4));
        }

        [Test]
        public void return_correct_min_element_and_remove_it_from_heap_on_delete_min_operation()
        {
            _heap.Add(7);
            _heap.Add(5);
            _heap.Add(3);

            Assert.That(_heap.DeleteMin(), Is.EqualTo(3));
            Assert.That(_heap.Count, Is.EqualTo(2));
        }

        [Test]
        public void return_elements_in_sorted_order()
        {
            var reference = new[] {2, 5, 7, 9, 11, 27, 32};
            var returned = new List<int>();

            for (int i=reference.Length-1; i>=0; --i)
            {
                _heap.Add(reference[i]);
            }

            while (_heap.Count > 0)
            {
                returned.Add(_heap.DeleteMin());
            }

            Assert.That(returned, Is.EquivalentTo(reference));
        }

        [Test]
        public void keep_all_duplicates()
        {
            var reference = new[] { 2, 5, 5, 7, 9, 9, 11, 11, 11, 27, 32 };
            var returned = new List<int>();

            for (int i = reference.Length - 1; i >= 0; --i)
            {
                _heap.Add(reference[i]);
            }

            while (_heap.Count > 0)
            {
                returned.Add(_heap.DeleteMin());
            }

            Assert.That(returned, Is.EquivalentTo(reference));
        }

        public void handle_a_lot_of_elements_and_not_loose_any_elements()
        {
            var elements = new List<int>();
            var rnd = new Random(123456791);

            for (int i = 0; i < 1000; ++i)
            {
                var elem = rnd.Next();
                elements.Add(elem);   
                _heap.Add(elem);
            }

            elements.Sort();

            var returned = new List<int>();
            while (_heap.Count > 0)
            {
                returned.Add(_heap.DeleteMin());
            }
        }
    }
}