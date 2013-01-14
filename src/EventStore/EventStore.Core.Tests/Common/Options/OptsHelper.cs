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
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using Mono.Options;

namespace EventStore.Core.Tests.Common.Options
{
    public class OptsHelper
    {
        static OptsHelper()
        {
            TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressTypeConverter)));
        }

        public IEnumerable<OptionInfo> Options { get { throw new NotImplementedException(); } }

        private readonly string _envPrefix;
        private readonly OptionSet _optionSet;

        public OptsHelper(Expression<Func<string[]>> configs, string envPrefix)
        {
            _envPrefix = envPrefix;

            var configMember = MemberName(configs);

        }

        private static string MemberName<T>(Expression<Func<T>> expr)
        {
            Ensure.NotNull(expr, "expr");

            var memberExpr = expr.Body as MemberExpression;
            if (memberExpr == null)
                throw new ArgumentException("Given expression tree must represent MemberExpression.", "expr");

            return memberExpr.Member.Name;
        }

        public void RegisterFlag(Expression<Func<bool>> member, string cmdPrototype, string jsonPath, string envName, 
                                 bool? @default = null, string description = null)
        {
            throw new NotImplementedException();
        }

        public void Register<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                             T? @default = null, string description = null) 
            where T : struct
        {
            throw new NotImplementedException();
        }

        public void RegisterRef<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                                   T @default = null, string description = null) 
            where T : class
        {
            throw new NotImplementedException();
        }

        public void RegisterArray<T>(Expression<Func<T[]>> member, string cmdPrototype, string jsonPath, 
                                     string envName, string envSeparator,
                                     T[] @default = null, string description = null)
        {
            throw new NotImplementedException();
        }

        public void Parse(params string[] args)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(Expression<Func<T>> member)
        {
            throw new NotImplementedException();
        }

        public string GetUsage()
        {
            using (var stream = new StringWriter())
            {
                stream.WriteLine("Usage:");
                _optionSet.WriteOptionDescriptions(stream);
                return stream.ToString();
            }
        }

        public string DumpOptions()
        {
            var sb = new StringBuilder();
            foreach (var option in Options)
            {
                sb.AppendFormat("{0}: {1} ({2} at {3}{4})",
                                option.Name.ToUpper().Replace("_", " "),
                                option.Value,
                                option.OriginOptionName,
                                option.Origin,
                                option.Origin == OptionOrigin.Config ? option.OriginName : string.Empty);
            }
            return sb.ToString();
        }
    }
}