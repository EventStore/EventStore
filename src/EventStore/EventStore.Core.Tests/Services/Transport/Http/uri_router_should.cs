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
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Http
{
    [TestFixture]
    public class naive_uri_router_should: uri_router_should
    {
        public naive_uri_router_should() : base(() => new NaiveUriRouter())
        {
        }
    }

    [TestFixture]
    public class trie_uri_router_should : uri_router_should
    {
        public trie_uri_router_should()
            : base(() => new TrieUriRouter())
        {
        }
    }

    public abstract class uri_router_should
    {
        private readonly Func<IUriRouter> _uriRouterFactory;

        private IUriRouter _router;

        protected uri_router_should(Func<IUriRouter> uriRouterFactory)
        {
            Ensure.NotNull(uriRouterFactory, "uriRouterFactory");
            _uriRouterFactory = uriRouterFactory;
        }

        [SetUp]
        public void SetUp()
        {
            _router = _uriRouterFactory();

            _router.RegisterControllerAction(new ControllerAction("/", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/{placeholder}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/halt", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/streams/{stream}/{event}/backward/{count}?embed={embed}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/s/stats/{*statPath}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/streams/$all/", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/streams/$$all", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/streams/$mono?param={param}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });

            _router.RegisterControllerAction(new ControllerAction("/streams/test", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/streams/test", HttpMethod.Post, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });

            _router.RegisterControllerAction(new ControllerAction("/t/{placeholder1}/{placholder2}/{placeholder3}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/{placeholder1}/{placholder2}/something", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/{placeholder1}/something/{placeholder3}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/{placeholder1}/something/something", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/something/{placholder2}/{placeholder3}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/something/{placholder2}/something", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/something/something/{placeholder3}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
            _router.RegisterControllerAction(new ControllerAction("/t/something/something/something", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });
        }

        [Test]
        public void detect_duplicate_route()
        {
            Assert.That(() => 
                _router.RegisterControllerAction(new ControllerAction("/halt", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { }),
                Throws.Exception.InstanceOf<ArgumentException>().With.Message.EqualTo("Duplicate route."));
        }

        [Test]
        public void match_root()
        {
            var match = _router.GetAllUriMatches(Uri("/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test]
        public void match_single_segment_path()
        {
            var match = _router.GetAllUriMatches(Uri("/halt"));
            Assert.AreEqual(2, match.Count);
            Assert.AreEqual("/{placeholder}", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
            Assert.AreEqual("/halt", match[1].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[1].ControllerAction.HttpMethod);
        }

        [Test, Ignore]
        public void not_care_about_trailing_slash()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/$all"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$all/", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);

            match = _router.GetAllUriMatches(Uri("/streams/$all/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$all/", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test, Ignore]
        public void not_care_about_trailing_slash2()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/$$all"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$$all", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);

            match = _router.GetAllUriMatches(Uri("/streams/$$all/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$$all", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test]
        public void care_about_trailing_slash()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/$all/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$all/", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);

            match = _router.GetAllUriMatches(Uri("/streams/$all"));
            Assert.AreEqual(0, match.Count);
        }

        [Test]
        public void care_about_trailing_slash2()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/$$all"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$$all", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);

            match = _router.GetAllUriMatches(Uri("/streams/$$all/"));
            Assert.AreEqual(0, match.Count);
        }

        [Test]
        public void match_route_with_dollar_sign()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/$mono"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$mono?param={param}", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);

            match = _router.GetAllUriMatches(Uri("/streams/$mono?param=bla"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/$mono?param={param}", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test]
        public void match_complex_route_with_placeholders_and_query_params()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/test-stream/10/backward/20?embed=true"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/{stream}/{event}/backward/{count}?embed={embed}", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test]
        public void match_complex_route_with_placeholders_and_query_params_when_no_query_params_are_set()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/test-stream/head/backward/20"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/streams/{stream}/{event}/backward/{count}?embed={embed}", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
        }

        [Test]
        public void not_match_partial_route_match()
        {
            var match = _router.GetAllUriMatches(Uri("/projection/proj/something"));
            Assert.AreEqual(0, match.Count);
        }

        [Test]
        public void match_all_possible_alternatives()
        {
            var match = _router.GetAllUriMatches(Uri("/t/something/something/something"));
            Assert.AreEqual(8, match.Count);
            Assert.AreEqual(new[]
                            {
                                    "/t/{placeholder1}/{placholder2}/{placeholder3}",
                                    "/t/{placeholder1}/{placholder2}/something",
                                    "/t/{placeholder1}/something/{placeholder3}",
                                    "/t/{placeholder1}/something/something",
                                    "/t/something/{placholder2}/{placeholder3}",
                                    "/t/something/{placholder2}/something",
                                    "/t/something/something/{placeholder3}",
                                    "/t/something/something/something"
                            },
                            match.Select(x => x.ControllerAction.UriTemplate).ToArray());
        }

        [Test]
        public void match_same_routes_with_different_http_methods()
        {
            var match = _router.GetAllUriMatches(Uri("/streams/test"));
            Assert.AreEqual(2, match.Count);
            Assert.AreEqual("/streams/test", match[0].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Get, match[0].ControllerAction.HttpMethod);
            Assert.AreEqual("/streams/test", match[1].ControllerAction.UriTemplate);
            Assert.AreEqual(HttpMethod.Post, match[1].ControllerAction.HttpMethod);
        }

        [Test]
        public void match_greedy_route_with_bare_minimum_of_uri()
        {
            var match = _router.GetAllUriMatches(Uri("/s/stats/test"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/s/stats/{*statPath}", match[0].ControllerAction.UriTemplate);
        }

        [Test]
        public void match_greedy_route_and_catch_long_uri()
        {
            var match = _router.GetAllUriMatches(Uri("/s/stats/some/long/stat/path"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/s/stats/{*statPath}", match[0].ControllerAction.UriTemplate);
        }

        [Test]
        public void match_greedy_route_with_empty_path_part_starting_with_slash()
        {
            var match = _router.GetAllUriMatches(Uri("/s/stats/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/s/stats/{*statPath}", match[0].ControllerAction.UriTemplate);
        }

        [Test]
        public void not_match_greedy_route_with_empty_path_part_without_slash()
        {
            var match = _router.GetAllUriMatches(Uri("/s/stats"));
            Assert.AreEqual(0, match.Count);
        }

        [Test]
        public void match_greedy_route_in_the_root_to_any_path()
        {
            var tmpRouter = _uriRouterFactory();
            tmpRouter.RegisterControllerAction(new ControllerAction("/{*greedy}", HttpMethod.Get, Codec.NoCodecs, FakeController.SupportedCodecs), (x, y) => { });

            var match = tmpRouter.GetAllUriMatches(Uri("/"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/{*greedy}", match[0].ControllerAction.UriTemplate);

            match = tmpRouter.GetAllUriMatches(Uri("/something"));
            Assert.AreEqual(1, match.Count);
            Assert.AreEqual("/{*greedy}", match[0].ControllerAction.UriTemplate);
        }

        private Uri Uri(string relativePath)
        {
            return new Uri("http://localhost:12345" + relativePath);
        }
    }
}