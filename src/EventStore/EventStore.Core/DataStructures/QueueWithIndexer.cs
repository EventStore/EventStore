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

namespace EventStore.Core.DataStructures
{
    public class QueueWithIndexer<T>
    {
        private static readonly T[] EmptyArray = new T[0];
        private T[] _array;
        private int _head;
        private int _tail;
        private int _size;

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </summary>
        /// 
        /// <returns>
        /// The number of elements contained in the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </returns>
        public int Count
        {
            get
            {
                return _size;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.Queue`1"/> class that is empty and has the default initial capacity.
        /// </summary>
        public QueueWithIndexer()
        {
            _array = EmptyArray;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.Queue`1"/> class that is empty and has the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial number of elements that the <see cref="T:System.Collections.Generic.Queue`1"/> can contain.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="capacity"/> is less than zero.</exception>
        public QueueWithIndexer(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");
            _array = new T[capacity];
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Collections.Generic.Queue`1"/> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new <see cref="T:System.Collections.Generic.Queue`1"/>.</param><exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is null.</exception>
        public QueueWithIndexer(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            _array = new T[4];
            _size = 0;
            foreach (var obj in collection)
                Enqueue(obj);
        }

        /// <summary>
        /// Removes all objects from the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </summary>
        /// <filterpriority>1</filterpriority>
        public void Clear()
        {
            if (_head < _tail)
            {
                Array.Clear(_array, _head, _size);
            }
            else
            {
                Array.Clear(_array, _head, _array.Length - _head);
                Array.Clear(_array, 0, _tail);
            }
            _head = 0;
            _tail = 0;
            _size = 0;
        }

        /// <summary>
        /// Copies the <see cref="T:System.Collections.Generic.Queue`1"/> elements to an existing one-dimensional <see cref="T:System.Array"/>, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.Queue`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than zero.</exception><exception cref="T:System.ArgumentException">The number of elements in the source <see cref="T:System.Collections.Generic.Queue`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException("arrayIndex");
            int length1 = array.Length;
            if (length1 - arrayIndex < _size)
                throw new ArgumentException("Invalid offset.");
            int num = length1 - arrayIndex < _size ? length1 - arrayIndex : _size;
            if (num == 0)
                return;
            int length2 = _array.Length - _head < num ? _array.Length - _head : num;
            Array.Copy(_array, _head, array, arrayIndex, length2);
            int length3 = num - length2;
            if (length3 <= 0)
                return;
            Array.Copy(_array, 0, array, arrayIndex + _array.Length - _head, length3);
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.Queue`1"/>. The value can be null for reference types.</param>
        public void Enqueue(T item)
        {
            if (_size == _array.Length)
            {
                int capacity = (int)(_array.Length * 200L / 100L);
                if (capacity < _array.Length + 4)
                    capacity = _array.Length + 4;
                SetCapacity(capacity);
            }
            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            ++_size;
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </summary>
        /// 
        /// <returns>
        /// The object that is removed from the beginning of the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="T:System.Collections.Generic.Queue`1"/> is empty.</exception>
        public T Dequeue()
        {
            if (_size == 0)
                throw new InvalidOperationException("Empty queue.");

            T obj = _array[_head];
            _array[_head] = default(T);
            _head = (_head + 1) % _array.Length;
            --_size;
            return obj;
        }

        /// <summary>
        /// Returns the object at the beginning of the <see cref="T:System.Collections.Generic.Queue`1"/> without removing it.
        /// </summary>
        /// 
        /// <returns>
        /// The object at the beginning of the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="T:System.Collections.Generic.Queue`1"/> is empty.</exception>
        public T Peek()
        {
            if (_size == 0)
                throw new InvalidOperationException("Empty queue");
            return _array[_head];
        }

        public T PeekLast()
        {
            if (_size == 0)
                throw new InvalidOperationException("Empty queue");
            return this[_size - 1];
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _size)
                    throw new ArgumentOutOfRangeException("index");
                var i = _head + index;
                return _array[i >= _array.Length ? i - _array.Length : i];
            }
        }

        public Tuple<T, int> BinarySearch(T value, Func<T, T, int> compare)
        {
            var length = _array.Length;

            int l = 0;
            int r = _size - 1;
            while (l <= r)
            {
                int m = l + (r - l)/2;

                var i = _head + m;
                var elem = _array[i >= length ? i - length : i];

                var cmp = compare(elem, value);

                if (cmp == 0)
                    return Tuple.Create(elem, m);

                if (cmp < 0)
                    l = m + 1;
                else
                    r = m - 1;
            }

            return Tuple.Create(default(T), -1);
        }

        /// <summary>
        /// Copies the <see cref="T:System.Collections.Generic.Queue`1"/> elements to a new array.
        /// </summary>
        /// 
        /// <returns>
        /// A new array containing elements copied from the <see cref="T:System.Collections.Generic.Queue`1"/>.
        /// </returns>
        public T[] ToArray()
        {
            T[] objArray = new T[_size];
            if (_size == 0)
                return objArray;
            if (_head < _tail)
            {
                Array.Copy(_array, _head, objArray, 0, _size);
            }
            else
            {
                Array.Copy(_array, _head, objArray, 0, _array.Length - _head);
                Array.Copy(_array, 0, objArray, _array.Length - _head, _tail);
            }
            return objArray;
        }

        private void SetCapacity(int capacity)
        {
            T[] objArray = new T[capacity];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, objArray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, objArray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, objArray, _array.Length - _head, _tail);
                }
            }
            _array = objArray;
            _head = 0;
            _tail = _size == capacity ? 0 : _size;
        }

        /// <summary>
        /// Sets the capacity to the actual number of elements in the <see cref="T:System.Collections.Generic.Queue`1"/>, if that number is less than 90 percent of current capacity.
        /// </summary>
        public void TrimExcess()
        {
            if (_size >= (int)(_array.Length * 0.9))
                return;
            SetCapacity(_size);
        }
    }
}
