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

using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core;
using EventStore.Web.Users;

namespace EventStore.Integration.Tests.Helpers
{
    class MediumNode: MiniNode
    {
        public static MediumNode Create(string pathname, bool runProjections)
        {
            var projectionsSubsystem = new ProjectionsSubsystem(
                    projectionWorkerThreadCount: 1,
                    runProjections: runProjections ? RunProjections.All : RunProjections.System);
            return new MediumNode(pathname, projectionsSubsystem, runProjections);
        }

        private Projections.Core.ProjectionsSubsystem _projections;

        private MediumNode(string pathname, ProjectionsSubsystem projectionsSubsystem, bool runProjections)
            : base(pathname, subsystems: new []{projectionsSubsystem})
        {
            _projections = projectionsSubsystem;
            RegisterWebControllers(new[] {NodeSubsystems.Projections} );
            RegisterUIProjections();
}

        private void RegisterUIProjections()
        {
            var users = new UserManagementProjectionsRegistration();
            Node.MainBus.Subscribe(users);
        }

        private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems)
        {
            Node.HttpService.SetupController(new WebSiteController(Node.MainQueue, enabledNodeSubsystems));
            Node.HttpService.SetupController(new UsersWebController(Node.MainQueue));
        }
    }
}
