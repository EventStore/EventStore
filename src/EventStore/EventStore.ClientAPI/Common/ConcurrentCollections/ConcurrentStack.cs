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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace EventStore.ClientAPI.Common.ConcurrentCollections
{
    /// <summary>
    /// Not very concurrent stack for use in mono
    /// </summary>
    /// <typeparam name="T"></typeparam>
  public class ConcurrentStack<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
  {
      private readonly Stack<T> _stack = new Stack<T>();
      private readonly object _padLock = new object();

      public IEnumerator<T> GetEnumerator()
      {
          return _stack.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
          return GetEnumerator();
      }

      public void CopyTo(Array array, int index)
      {
          throw new NotImplementedException();
      }

      public int Count
      {
          get
          {
              if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
              try
              {
                  return _stack.Count;
              }
              finally
              {
                  Monitor.Exit(_padLock);
              }
          }
      }

      public object SyncRoot
      {
          get { return _padLock; }
      }

      public bool IsSynchronized
      {
          get { return true; }
      }

      public void CopyTo(T[] array, int index)
      {
          if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
          try
          {
              _stack.CopyTo(array, index);
          }
          finally
          {
              Monitor.Exit(_padLock);
          }
      }

      public bool TryAdd(T item)
      {
          if (!Monitor.TryEnter(_padLock, 5000)) return false;
          try
          {
              _stack.Push(item);
              return true;
          }
          finally
          {
              Monitor.Exit(_padLock);
          }
      }

      public void Push(T item)
      {
          TryAdd(item);
      }

      public bool TryPop(out T item)
      {
          return TryTake(out item);
      }

      public bool TryTake(out T item)
      {
          item = default(T);
          if (!Monitor.TryEnter(_padLock, 5000)) return false;
          try
          {
				if(_stack.Count == 0) return false;
              item = _stack.Pop();
              return true;
          }
          finally
          {
              Monitor.Exit(_padLock);
          }
      }

      public T[] ToArray()
      {
          if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
          try
          {
              return _stack.ToArray();
          }
          finally
          {
              Monitor.Exit(_padLock);
          }
      }
  }
}
