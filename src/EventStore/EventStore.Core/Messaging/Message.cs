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
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace EventStore.Core.Messaging
{
    public abstract class Message
    {
/*
        protected static int NextMsgId = -1;

        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public virtual int MsgTypeId { get { return TypeId; } }
 */
    }

    public static class MessageHierarchy
    {
        public static readonly Dictionary<Type, List<Type>> Descendants;
//        public static readonly int[][] Descendants2;
//        public static readonly int MaxMsgTypeId;

        static MessageHierarchy()
        {
            var sw = Stopwatch.StartNew();

/*
            var dict = new Dictionary<int, List<int>>();
            var idDict = new Dictionary<Type, int>();
*/
            var rootMsgType = typeof(Message);

            Descendants = new Dictionary<Type, List<Type>>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var msgType in assembly.GetTypes().Where(rootMsgType.IsAssignableFrom))
                {
/*
                    var msgTypeId = GetMsgTypeId(msgType);
                    idDict.Add(msgType, msgTypeId);
                    MaxMsgTypeId = Math.Max(msgTypeId, MaxMsgTypeId);

                    Console.WriteLine("Found {0} with MsgTypeId {1}", msgType.Name, msgTypeId);
*/

                    var type = msgType;
                    while (true)
                    {
/*
                        var typeId = GetMsgTypeId(type);
                        List<int> list;
                        if (!dict.TryGetValue(typeId, out list))
                        {
                            list = new List<int>();
                            dict.Add(typeId, list);
                        }
                        list.Add(msgTypeId);
*/

                        List<Type> typeList;
                        if (!Descendants.TryGetValue(type, out typeList))
                        {
                            typeList = new List<Type>();
                            Descendants.Add(type, typeList);
                        }
                        typeList.Add(msgType);

                        if (type == rootMsgType)
                            break;
                        type = type.BaseType;
                    };
                }
            }

/*
            Descendants2 = new int[MaxMsgTypeId + 1][];
            for (int i = 0; i <= MaxMsgTypeId; ++i)
            {
                var list = dict[i];
                Descendants2[i] = new int[list.Count];
                for (int j = 0; j < list.Count; ++j)
                {
                    Descendants2[i][j] = list[j];
                }
            }
*/

            Console.WriteLine("MessageHierarchy initialization took {0}.", sw.Elapsed);
        }

        private static int GetMsgTypeId(Type msgType)
        {
            var msgTypeField = msgType.GetFields(BindingFlags.Static | BindingFlags.NonPublic).First(x => x.Name == "TypeId");
            var msgTypeId = (int)msgTypeField.GetValue(null);
            return msgTypeId;
        }
    }

}
