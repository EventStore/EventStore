﻿using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal class SocketArgsPool
    {
        public readonly string Name;
        private readonly Func<SocketAsyncEventArgs> _socketArgsCreator;

        private readonly ConcurrentStack<SocketAsyncEventArgs> _socketArgsPool = new ConcurrentStack<SocketAsyncEventArgs>();

        public SocketArgsPool(string name, int initialCount, Func<SocketAsyncEventArgs> socketArgsCreator)
        {
            if (socketArgsCreator == null)
                throw new ArgumentNullException("socketArgsCreator");
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException("initialCount");

            Name = name;
            _socketArgsCreator = socketArgsCreator;

            for (int i = 0; i < initialCount; ++i)
            {
                _socketArgsPool.Push(socketArgsCreator());
            }
        }

        public SocketAsyncEventArgs Get()
        {
            SocketAsyncEventArgs result;
            if (_socketArgsPool.TryPop(out result))
                return result;
            return _socketArgsCreator();
        }

        public void Return(SocketAsyncEventArgs socketArgs)
        {
            _socketArgsPool.Push(socketArgs);
        }
    }
}