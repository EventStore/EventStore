// Type: System.Collections.Concurrent.ConcurrentQueue`1
// Assembly: mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;

#pragma warning disable 420

namespace EventStore.ClientAPI.Common.ConcurrentCollections
{
  /// <summary>
  /// Represents a thread-safe first in-first out (FIFO) collection.
  /// </summary>
  /// <typeparam name="T">The type of the elements contained in the queue.</typeparam>
  [ComVisible(false)]
  [DebuggerDisplay("Count = {Count}")]
  [Serializable]
  [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true, Synchronization = true)]
  public class ConcurrentQueue<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
  {
    private const int SEGMENT_SIZE = 32;
    [NonSerialized]
    private volatile ConcurrentQueue<T>.Segment m_head;
    [NonSerialized]
    private volatile ConcurrentQueue<T>.Segment m_tail;
    private T[] m_serializationArray;

    bool ICollection.IsSynchronized
    {
      get
      {
        return false;
      }
    }

    object ICollection.SyncRoot
    {
      get
      {
        throw new NotSupportedException("ConcurrentCollection_SyncRoot_NotSupported");
      }
    }

    /// <summary>
    /// Gets a value that indicates whether the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> is empty.
    /// </summary>
    /// 
    /// <returns>
    /// true if the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> is empty; otherwise, false.
    /// </returns>
    public bool IsEmpty
    {
      get
      {
        ConcurrentQueue<T>.Segment segment = this.m_head;
        if (!segment.IsEmpty)
          return false;
        if (segment.Next == null)
          return true;
        SpinWait spinWait = new SpinWait();
        for (; segment.IsEmpty; segment = this.m_head)
        {
          if (segment.Next == null)
            return true;
          spinWait.SpinOnce();
        }
        return false;
      }
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// The number of elements contained in the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </returns>
    public int Count
    {
      get
      {
        ConcurrentQueue<T>.Segment head;
        ConcurrentQueue<T>.Segment tail;
        int headLow;
        int tailHigh;
        this.GetHeadTailPositions(out head, out tail, out headLow, out tailHigh);
        if (head == tail)
          return tailHigh - headLow + 1;
        else
          return 32 - headLow + 32 * (int) (tail.m_index - head.m_index - 1L) + (tailHigh + 1);
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> class.
    /// </summary>
    public ConcurrentQueue()
    {
      this.m_head = this.m_tail = new ConcurrentQueue<T>.Segment(0L);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.</param><exception cref="T:System.ArgumentNullException">The <paramref name="collection"/> argument is null.</exception>
    public ConcurrentQueue(IEnumerable<T> collection)
    {
      if (collection == null)
        throw new ArgumentNullException("collection");
      this.InitializeFromCollection(collection);
    }

    private void InitializeFromCollection(IEnumerable<T> collection)
    {
      this.m_head = this.m_tail = new ConcurrentQueue<T>.Segment(0L);
      int num = 0;
      foreach (T obj in collection)
      {
        this.m_tail.UnsafeAdd(obj);
        ++num;
        if (num >= 32)
        {
          this.m_tail = this.m_tail.UnsafeGrow();
          num = 0;
        }
      }
    }

    [OnSerializing]
    private void OnSerializing(StreamingContext context)
    {
      this.m_serializationArray = this.ToArray();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
      this.InitializeFromCollection((IEnumerable<T>) this.m_serializationArray);
      this.m_serializationArray = (T[]) null;
    }

    void ICollection.CopyTo(Array array, int index)
    {
      if (array == null)
        throw new ArgumentNullException("array");
      ((ICollection)this.ToList()).CopyTo(array, index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return (IEnumerator) this.GetEnumerator();
    }

    bool IProducerConsumerCollection<T>.TryAdd(T item)
    {
      this.Enqueue(item);
      return true;
    }

    bool IProducerConsumerCollection<T>.TryTake(out T item)
    {
      return this.TryDequeue(out item);
    }

    /// <summary>
    /// Copies the elements stored in the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> to a new array.
    /// </summary>
    /// 
    /// <returns>
    /// A new array containing a snapshot of elements copied from the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </returns>
    public T[] ToArray()
    {
      return this.ToList().ToArray();
    }

    private List<T> ToList()
    {
      ConcurrentQueue<T>.Segment head;
      ConcurrentQueue<T>.Segment tail;
      int headLow;
      int tailHigh;
      this.GetHeadTailPositions(out head, out tail, out headLow, out tailHigh);
      if (head == tail)
        return head.ToList(headLow, tailHigh);
      List<T> list = new List<T>((IEnumerable<T>) head.ToList(headLow, 31));
      for (ConcurrentQueue<T>.Segment next = head.Next; next != tail; next = next.Next)
        list.AddRange((IEnumerable<T>) next.ToList(0, 31));
      list.AddRange((IEnumerable<T>) tail.ToList(0, tailHigh));
      return list;
    }

    private void GetHeadTailPositions(out ConcurrentQueue<T>.Segment head, out ConcurrentQueue<T>.Segment tail, out int headLow, out int tailHigh)
    {
      head = this.m_head;
      tail = this.m_tail;
      headLow = head.Low;
      tailHigh = tail.High;
      SpinWait spinWait = new SpinWait();
      while (head != this.m_head || tail != this.m_tail || (headLow != head.Low || tailHigh != tail.High) || head.m_index > tail.m_index)
      {
        spinWait.SpinOnce();
        head = this.m_head;
        tail = this.m_tail;
        headLow = head.Low;
        tailHigh = tail.High;
      }
    }

    /// <summary>
    /// Copies the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> elements to an existing one-dimensional <see cref="T:System.Array"/>, starting at the specified array index.
    /// </summary>
    /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in Visual Basic).</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.</exception><exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than the length of the <paramref name="array"/> -or- The number of elements in the source <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.</exception>
    public void CopyTo(T[] array, int index)
    {
      if (array == null)
        throw new ArgumentNullException("array");
      this.ToList().CopyTo(array, index);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// An enumerator for the contents of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
      return (IEnumerator<T>) this.ToList().GetEnumerator();
    }

    /// <summary>
    /// Adds an object to the end of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </summary>
    /// <param name="item">The object to add to the end of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>. The value can be a null reference (Nothing in Visual Basic) for reference types.</param>
    public void Enqueue(T item)
    {
      SpinWait spinWait = new SpinWait();
      while (!this.m_tail.TryAppend(item, ref this.m_tail))
        spinWait.SpinOnce();
    }

    /// <summary>
    /// Attempts to remove and return the object at the beginning of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// true if an element was removed and returned from the beggining of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> succesfully; otherwise, false.
    /// </returns>
    /// <param name="result">When this method returns, if the operation was successful, <paramref name="result"/> contains the object removed. If no object was available to be removed, the value is unspecified.</param>
    public bool TryDequeue(out T result)
    {
      while (!this.IsEmpty)
      {
        if (this.m_head.TryRemove(out result, ref this.m_head))
          return true;
      }
      result = default (T);
      return false;
    }

    /// <summary>
    /// Attempts to return an object from the beginning of the <see cref="T:System.Collections.Concurrent.ConcurrentQueue`1"/> without removing it.
    /// </summary>
    /// 
    /// <returns>
    /// true if and object was returned successfully; otherwise, false.
    /// </returns>
    /// <param name="result">When this method returns, <paramref name="result"/> contains an object from the beginning of the <see cref="T:System.Collections.Concurrent.ConccurrentQueue{T}"/> or an unspecified value if the operation failed.</param>
    public bool TryPeek(out T result)
    {
      while (!this.IsEmpty)
      {
        if (this.m_head.TryPeek(out result))
          return true;
      }
      result = default (T);
      return false;
    }

    private class Segment
    {
      internal volatile T[] m_array;
      private volatile int[] m_state;
      private volatile ConcurrentQueue<T>.Segment m_next;
      internal readonly long m_index;
      private volatile int m_low;
      private volatile int m_high;

      internal ConcurrentQueue<T>.Segment Next
      {
        get
        {
          return this.m_next;
        }
      }

      internal bool IsEmpty
      {
        get
        {
          return this.Low > this.High;
        }
      }

      internal int Low
      {
        get
        {
          return Math.Min(this.m_low, 32);
        }
      }

      internal int High
      {
        get
        {
          return Math.Min(this.m_high, 31);
        }
      }

      internal Segment(long index)
      {
        this.m_array = new T[32];
        this.m_state = new int[32];
        this.m_high = -1;
        this.m_index = index;
      }

      internal void UnsafeAdd(T value)
      {
        ++this.m_high;
        this.m_array[this.m_high] = value;
        this.m_state[this.m_high] = 1;
      }

      internal ConcurrentQueue<T>.Segment UnsafeGrow()
      {
        ConcurrentQueue<T>.Segment segment = new ConcurrentQueue<T>.Segment(this.m_index + 1L);
        this.m_next = segment;
        return segment;
      }

      internal void Grow(ref ConcurrentQueue<T>.Segment tail)
      {
        this.m_next = new ConcurrentQueue<T>.Segment(this.m_index + 1L);
        tail = this.m_next;
      }

      internal bool TryAppend(T value, ref ConcurrentQueue<T>.Segment tail)
      {
        if (this.m_high >= 31)
          return false;
        int index = 32;
        try
        {
        }
        finally
        {
          index = Interlocked.Increment(ref this.m_high);
          if (index <= 31)
          {
            this.m_array[index] = value;
            this.m_state[index] = 1;
          }
          if (index == 31)
            this.Grow(ref tail);
        }
        return index <= 31;
      }

      internal bool TryRemove(out T result, ref ConcurrentQueue<T>.Segment head)
      {
        SpinWait spinWait1 = new SpinWait();
        int low = this.Low;
        for (int high = this.High; low <= high; high = this.High)
        {
          if (Interlocked.CompareExchange(ref this.m_low, low + 1, low) == low)
          {
            SpinWait spinWait2 = new SpinWait();
            while (this.m_state[low] == 0)
              spinWait2.SpinOnce();
            result = this.m_array[low];
            if (low + 1 >= 32)
            {
              SpinWait spinWait3 = new SpinWait();
              while (this.m_next == null)
                spinWait3.SpinOnce();
              head = this.m_next;
            }
            return true;
          }
          else
          {
            spinWait1.SpinOnce();
            low = this.Low;
          }
        }
        result = default (T);
        return false;
      }

      internal bool TryPeek(out T result)
      {
        result = default (T);
        int low = this.Low;
        if (low > this.High)
          return false;
        SpinWait spinWait = new SpinWait();
        while (this.m_state[low] == 0)
          spinWait.SpinOnce();
        result = this.m_array[low];
        return true;
      }

      internal List<T> ToList(int start, int end)
      {
        List<T> list = new List<T>();
        for (int index = start; index <= end; ++index)
        {
          SpinWait spinWait = new SpinWait();
          while (this.m_state[index] == 0)
            spinWait.SpinOnce();
          list.Add(this.m_array[index]);
        }
        return list;
      }
    }
  }
}
