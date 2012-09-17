// Type: System.Collections.Concurrent.ConcurrentStack`1
// Assembly: mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Threading;

#pragma warning disable 420

namespace EventStore.Common.ConcurrentCollections
{
  /// <summary>
  /// Represents a thread-safe last in-first out (LIFO) collection.
  /// </summary>
  /// <typeparam name="T">The type of the elements contained in the stack.</typeparam>
  [DebuggerDisplay("Count = {Count}")]
  [Serializable]
  [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true, Synchronization = true)]
  public class ConcurrentStack<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
  {
    private const int BACKOFF_MAX_YIELDS = 8;
    [NonSerialized]
    private volatile ConcurrentStack<T>.Node m_head;
    private T[] m_serializationArray;

    /// <summary>
    /// Gets a value that indicates whether the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> is empty.
    /// </summary>
    /// 
    /// <returns>
    /// true if the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> is empty; otherwise, false.
    /// </returns>
    public bool IsEmpty
    {
      get
      {
        return this.m_head == null;
      }
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// The number of elements contained in the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </returns>
    public int Count
    {
      get
      {
        int num = 0;
        for (ConcurrentStack<T>.Node node = this.m_head; node != null; node = node.m_next)
          ++num;
        return num;
      }
    }

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
    /// Initializes a new instance of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> class.
    /// </summary>
    public ConcurrentStack()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><exception cref="T:System.ArgumentNullException">The <paramref name="collection"/> argument is null.</exception>
    public ConcurrentStack(IEnumerable<T> collection)
    {
      if (collection == null)
        throw new ArgumentNullException("collection");
      this.InitializeFromCollection(collection);
    }

    private void InitializeFromCollection(IEnumerable<T> collection)
    {
      ConcurrentStack<T>.Node node = (ConcurrentStack<T>.Node) null;
      foreach (T obj in collection)
        node = new ConcurrentStack<T>.Node(obj)
        {
          m_next = node
        };
      this.m_head = node;
    }

    [OnSerializing]
    private void OnSerializing(StreamingContext context)
    {
      this.m_serializationArray = this.ToArray();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
      ConcurrentStack<T>.Node node1 = (ConcurrentStack<T>.Node) null;
      ConcurrentStack<T>.Node node2 = (ConcurrentStack<T>.Node) null;
      for (int index = 0; index < this.m_serializationArray.Length; ++index)
      {
        ConcurrentStack<T>.Node node3 = new ConcurrentStack<T>.Node(this.m_serializationArray[index]);
        if (node1 == null)
          node2 = node3;
        else
          node1.m_next = node3;
        node1 = node3;
      }
      this.m_head = node2;
      this.m_serializationArray = (T[]) null;
    }

    /// <summary>
    /// Removes all objects from the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </summary>
    public void Clear()
    {
      this.m_head = (ConcurrentStack<T>.Node) null;
    }

    void ICollection.CopyTo(Array array, int index)
    {
        if (array == null)
            throw new ArgumentNullException("array");
        ((ICollection)this.ToList()).CopyTo(array, index);
    }

    /// <summary>
    /// Copies the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> elements to an existing one-dimensional <see cref="T:System.Array"/>, starting at the specified array index.
    /// </summary>
    /// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in Visual Basic).</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than zero.</exception><exception cref="T:System.ArgumentException"><paramref name="index"/> is equal to or greater than the length of the <paramref name="array"/> -or- The number of elements in the source <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> is greater than the available space from <paramref name="index"/> to the end of the destination <paramref name="array"/>.</exception>
    public void CopyTo(T[] array, int index)
    {
      if (array == null)
        throw new ArgumentNullException("array");
      this.ToList().CopyTo(array, index);
    }

    /// <summary>
    /// Inserts an object at the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </summary>
    /// <param name="item">The object to push onto the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>. The value can be a null reference (Nothing in Visual Basic) for reference types.</param>
    public void Push(T item)
    {
      ConcurrentStack<T>.Node node = new ConcurrentStack<T>.Node(item);
      node.m_next = this.m_head;
      if (Interlocked.CompareExchange<ConcurrentStack<T>.Node>(ref this.m_head, node, node.m_next) == node.m_next)
        return;
      this.PushCore(node, node);
    }

    /// <summary>
    /// Inserts multiple objects at the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> atomically.
    /// </summary>
    /// <param name="items">The objects to push onto the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><exception cref="T:System.ArgumentNullException"><paramref name="items"/> is a null reference (Nothing in Visual Basic).</exception>
    public void PushRange(T[] items)
    {
      if (items == null)
        throw new ArgumentNullException("items");
      this.PushRange(items, 0, items.Length);
    }

    /// <summary>
    /// Inserts multiple objects at the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> atomically.
    /// </summary>
    /// <param name="items">The objects to push onto the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><param name="startIndex">The zero-based offset in <paramref name="items"/> at which to begin inserting elements onto the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><param name="count">The number of elements to be inserted onto the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><exception cref="T:System.ArgumentNullException"><paramref name="items"/> is a null reference (Nothing in Visual Basic).</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="count"/> is negative. Or <paramref name="startIndex"/> is greater than or equal to the length of <paramref name="items"/>.</exception><exception cref="T:System.ArgumentException"><paramref name="startIndex"/> + <paramref name="count"/> is greater than the length of <paramref name="items"/>.</exception>
    public void PushRange(T[] items, int startIndex, int count)
    {
      this.ValidatePushPopRangeInput(items, startIndex, count);
      if (count == 0)
        return;
      ConcurrentStack<T>.Node tail;
      ConcurrentStack<T>.Node head = tail = new ConcurrentStack<T>.Node(items[startIndex]);
      for (int index = startIndex + 1; index < startIndex + count; ++index)
        head = new ConcurrentStack<T>.Node(items[index])
        {
          m_next = head
        };
      tail.m_next = this.m_head;
      if (Interlocked.CompareExchange<ConcurrentStack<T>.Node>(ref this.m_head, head, tail.m_next) == tail.m_next)
        return;
      this.PushCore(head, tail);
    }

    private void PushCore(ConcurrentStack<T>.Node head, ConcurrentStack<T>.Node tail)
    {
      SpinWait spinWait = new SpinWait();
      do
      {
        spinWait.SpinOnce();
        tail.m_next = this.m_head;
      }
      while (Interlocked.CompareExchange<ConcurrentStack<T>.Node>(ref this.m_head, head, tail.m_next) != tail.m_next);
    }

    private void ValidatePushPopRangeInput(T[] items, int startIndex, int count)
    {
      if (items == null)
        throw new ArgumentNullException("items");
      if (count < 0)
        throw new ArgumentOutOfRangeException("count", "ConcurrentStack_PushPopRange_CountOutOfRange");
      int length = items.Length;
      if (startIndex >= length || startIndex < 0)
        throw new ArgumentOutOfRangeException("startIndex", "ConcurrentStack_PushPopRange_StartOutOfRange");
      if (length - count < startIndex)
        throw new ArgumentException("ConcurrentStack_PushPopRange_InvalidCount");
    }

    bool IProducerConsumerCollection<T>.TryAdd(T item)
    {
      this.Push(item);
      return true;
    }

    /// <summary>
    /// Attempts to return an object from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> without removing it.
    /// </summary>
    /// 
    /// <returns>
    /// true if and object was returned successfully; otherwise, false.
    /// </returns>
    /// <param name="result">When this method returns, <paramref name="result"/> contains an object from the top of the <see cref="T:System.Collections.Concurrent.ConccurrentStack{T}"/> or an unspecified value if the operation failed.</param>
    public bool TryPeek(out T result)
    {
      ConcurrentStack<T>.Node node = this.m_head;
      if (node == null)
      {
        result = default (T);
        return false;
      }
      else
      {
        result = node.m_value;
        return true;
      }
    }

    /// <summary>
    /// Attempts to pop and return the object at the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// true if an element was removed and returned from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> succesfully; otherwise, false.
    /// </returns>
    /// <param name="result">When this method returns, if the operation was successful, <paramref name="result"/> contains the object removed. If no object was available to be removed, the value is unspecified.</param>
    public bool TryPop(out T result)
    {
      ConcurrentStack<T>.Node comparand = this.m_head;
      if (comparand == null)
      {
        result = default (T);
        return false;
      }
      else
      {
        if (Interlocked.CompareExchange<ConcurrentStack<T>.Node>(ref this.m_head, comparand.m_next, comparand) != comparand)
          return this.TryPopCore(out result);
        result = comparand.m_value;
        return true;
      }
    }

    /// <summary>
    /// Attempts to pop and return multiple objects from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> atomically.
    /// </summary>
    /// 
    /// <returns>
    /// The number of objects successfully popped from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> and inserted in <paramref name="items"/>.
    /// </returns>
    /// <param name="items">The <see cref="T:System.Array"/> to which objects popped from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> will be added.</param><exception cref="T:System.ArgumentNullException"><paramref name="items"/> is a null argument (Nothing in Visual Basic).</exception>
    public int TryPopRange(T[] items)
    {
      if (items == null)
        throw new ArgumentNullException("items");
      else
        return this.TryPopRange(items, 0, items.Length);
    }

    /// <summary>
    /// Attempts to pop and return multiple objects from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> atomically.
    /// </summary>
    /// 
    /// <returns>
    /// The number of objects successfully popped from the top of the stack and inserted in <paramref name="items"/>.
    /// </returns>
    /// <param name="items">The <see cref="T:System.Array"/> to which objects popped from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> will be added.</param><param name="startIndex">The zero-based offset in <paramref name="items"/> at which to begin inserting elements from the top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.</param><param name="count">The number of elements to be popped from top of the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> and inserted into <paramref name="items"/>.</param><exception cref="T:System.ArgumentNullException"><paramref name="items"/> is a null reference (Nothing in Visual Basic).</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="startIndex"/> or <paramref name="count"/> is negative. Or <paramref name="startIndex"/> is greater than or equal to the length of <paramref name="items"/>.</exception><exception cref="T:System.ArgumentException"><paramref name="startIndex"/> + <paramref name="count"/> is greater than the length of <paramref name="items"/>.</exception>
    public int TryPopRange(T[] items, int startIndex, int count)
    {
      this.ValidatePushPopRangeInput(items, startIndex, count);
      if (count == 0)
        return 0;
      ConcurrentStack<T>.Node poppedHead;
      int nodesCount = this.TryPopCore(count, out poppedHead);
      if (nodesCount > 0)
        this.CopyRemovedItems(poppedHead, items, startIndex, nodesCount);
      return nodesCount;
    }

    private bool TryPopCore(out T result)
    {
      ConcurrentStack<T>.Node poppedHead;
      if (this.TryPopCore(1, out poppedHead) == 1)
      {
        result = poppedHead.m_value;
        return true;
      }
      else
      {
        result = default (T);
        return false;
      }
    }

    private int TryPopCore(int count, out ConcurrentStack<T>.Node poppedHead)
    {
      SpinWait spinWait = new SpinWait();
      int num1 = 1;
      Random random = new Random(Environment.TickCount & int.MaxValue);
      ConcurrentStack<T>.Node comparand;
      int num2;
      while (true)
      {
        comparand = this.m_head;
        if (comparand != null)
        {
          ConcurrentStack<T>.Node node = comparand;
          for (num2 = 1; num2 < count && node.m_next != null; ++num2)
            node = node.m_next;
          if (Interlocked.CompareExchange<ConcurrentStack<T>.Node>(ref this.m_head, node.m_next, comparand) != comparand)
          {
            for (int index = 0; index < num1; ++index)
              spinWait.SpinOnce();
            num1 = spinWait.NextSpinWillYield ? random.Next(1, 8) : num1 * 2;
          }
          else
            goto label_9;
        }
        else
          break;
      }
      poppedHead = (ConcurrentStack<T>.Node) null;
      return 0;
label_9:
      poppedHead = comparand;
      return num2;
    }

    private void CopyRemovedItems(ConcurrentStack<T>.Node head, T[] collection, int startIndex, int nodesCount)
    {
      ConcurrentStack<T>.Node node = head;
      for (int index = startIndex; index < startIndex + nodesCount; ++index)
      {
        collection[index] = node.m_value;
        node = node.m_next;
      }
    }

    bool IProducerConsumerCollection<T>.TryTake(out T item)
    {
      return this.TryPop(out item);
    }

    /// <summary>
    /// Copies the items stored in the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/> to a new array.
    /// </summary>
    /// 
    /// <returns>
    /// A new array containing a snapshot of elements copied from the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </returns>
    public T[] ToArray()
    {
      return this.ToList().ToArray();
    }

    private List<T> ToList()
    {
      List<T> list = new List<T>();
      for (ConcurrentStack<T>.Node node = this.m_head; node != null; node = node.m_next)
        list.Add(node.m_value);
      return list;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </summary>
    /// 
    /// <returns>
    /// An enumerator for the <see cref="T:System.Collections.Concurrent.ConcurrentStack`1"/>.
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
      return this.GetEnumerator(this.m_head);
    }

    private IEnumerator<T> GetEnumerator(ConcurrentStack<T>.Node head)
    {
      for (ConcurrentStack<T>.Node current = head; current != null; current = current.m_next)
        yield return current.m_value;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return (IEnumerator) this.GetEnumerator();
    }

    private class Node
    {
      internal T m_value;
      internal ConcurrentStack<T>.Node m_next;

      internal Node(T value)
      {
        this.m_value = value;
        this.m_next = (ConcurrentStack<T>.Node) null;
      }
    }
  }
}
