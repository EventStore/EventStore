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
using System.Threading;

namespace EventStore.Core.Messaging
{
    public abstract class Message
    {
        protected static int NextMsgId = -1;
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public virtual int MsgTypeId { get { return TypeId; } }
    }

    public static class MessageHierarchy
    {
        public static readonly Dictionary<Type, List<Type>> Descendants;
        public static readonly int[][] ParentsByTypeId;
        public static readonly int[][] DescendantsByTypeId;
        public static readonly Dictionary<Type, int[]> DescendantsByType;
        public static readonly Dictionary<Type, int> MsgTypeIdByType;
        public static readonly int MaxMsgTypeId;

        static MessageHierarchy()
        {
            var sw = Stopwatch.StartNew();

            MsgTypeIdByType = new Dictionary<Type, int>();
            var descendants = new Dictionary<int, List<int>>();
            var parents = new Dictionary<int, List<int>>();
            var rootMsgType = typeof(Message);

            Descendants = new Dictionary<Type, List<Type>>();

            int msgTypeCount = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var msgType in assembly.GetTypes().Where(rootMsgType.IsAssignableFrom))
                {
                    msgTypeCount += 1;

                    var msgTypeId = GetMsgTypeId(msgType);
                    MsgTypeIdByType.Add(msgType, msgTypeId);
                    parents.Add(msgTypeId, new List<int>());

                    MaxMsgTypeId = Math.Max(msgTypeId, MaxMsgTypeId);
                    //Console.WriteLine("Found {0} with MsgTypeId {1}", msgType.Name, msgTypeId);

                    var type = msgType;
                    while (true)
                    {
                        var typeId = GetMsgTypeId(type);
                        parents[msgTypeId].Add(typeId);
                        List<int> list;
                        if (!descendants.TryGetValue(typeId, out list))
                        {
                            list = new List<int>();
                            descendants.Add(typeId, list);
                        }
                        list.Add(msgTypeId);

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

            if (msgTypeCount - 1 != MaxMsgTypeId)
            {
                var wrongTypes = from typeId in MsgTypeIdByType
                                 group typeId.Key by typeId.Value into g
                                 where g.Count() > 1
                                 select new
                                 {
                                         TypeId = g.Key,
                                         MsgTypes = g.ToArray()
                                 };

                foreach (var wrongType in wrongTypes)
                {
                    Console.WriteLine("MsgTypeId {0} is assigned to type: {1}",
                                      wrongType.TypeId,
                                      string.Join(", ", wrongType.MsgTypes.Select(x => x.Name)));
                }

                throw new Exception("Incorrect Message Type IDs setup.");
            }

            DescendantsByTypeId = new int[MaxMsgTypeId + 1][];
            ParentsByTypeId = new int[MaxMsgTypeId + 1][];
            for (int i = 0; i <= MaxMsgTypeId; ++i)
            {
                var list = descendants[i];
                DescendantsByTypeId[i] = new int[list.Count];
                for (int j = 0; j < list.Count; ++j)
                {
                    DescendantsByTypeId[i][j] = list[j];
                }

                list = parents[i];
                ParentsByTypeId[i] = new int[list.Count];
                for (int j = 0; j < list.Count; ++j)
                {
                    ParentsByTypeId[i][j] = list[j];
                }
            }
            DescendantsByType = new Dictionary<Type, int[]>();
            foreach (var typeIdMap in MsgTypeIdByType)
            {
                DescendantsByType.Add(typeIdMap.Key, DescendantsByTypeId[typeIdMap.Value]);
            }

            Console.WriteLine("MessageHierarchy initialization took {0}.", sw.Elapsed);
        }

        private static int GetMsgTypeId(Type msgType)
        {
            int typeId;
            if (MsgTypeIdByType.TryGetValue(msgType, out typeId))
                return typeId;

            var msgTypeField = msgType.GetFields(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(x => x.Name == "TypeId");
            if (msgTypeField == null)
            {
                Console.WriteLine("Message {0} doesn't have TypeId field!", msgType.Name);
                throw new Exception(string.Format("Message {0} doesn't have TypeId field!", msgType.Name));
            }
            var msgTypeId = (int)msgTypeField.GetValue(null);
            return msgTypeId;
        }
    }

}
