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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace EventStore.UpgradeProjections
{
    class UpgradeProjectionsWorker
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private readonly bool _runUpgrade;
        private const string ProjectionCreatedOld = "ProjectionCreated";
        private const string ProjectionCreatedNew = "$ProjectionCreated";
        private const string ProjectionUpdatedOld = "ProjectionUpdated";
        private const string ProjectionUpdatedNew = "$ProjectionUpdated";
        private int _lastCatalogEventNumber;
        private readonly List<string> _oldStyleProjections;
        private readonly List<string> _existingProjections;
        private IEventStoreConnection _connection;
        private UserCredentials _credentials;
        private bool _upgradeRequired;

        public UpgradeProjectionsWorker(IPAddress ipAddress, int port, string userName, string password, bool runUpgrade)
        {
            _ipAddress = ipAddress;
            _port = port;
            _userName = userName;
            _password = password;
            _runUpgrade = runUpgrade;
            _oldStyleProjections = new List<string>();
            _existingProjections = new List<string>();
        }

        public void RunUpgrade()
        {
            Connect();
            LoadProjectionCatalog();
            UpgradeProjectionCatalog();
            UpgradeProjectionStreams();
            UpgradeProjectionCheckpoints();
            UpgradeProjectionPartitionCheckpoints();
            if (_upgradeRequired)
		        Log("*** Old style projection definitions found.  Run with --upgrade option");
        }

        private void UpgradeProjectionPartitionCheckpoints()
        {
            Log("Looking for projection partition checkpoint streams");
            var from = Position.Start;
            AllEventsSlice slice;
            do
            {
                slice = _connection.ReadAllEventsForward(@from, 100, false, _credentials);
                foreach (var @event in slice.Events)
                    if (@event.OriginalEventNumber == 0)
                        UpgradeStreamIfPartitionCheckpoint(@event.OriginalStreamId);
                from = slice.NextPosition;
            } while (!slice.IsEndOfStream);
            Log("Completed looking for partition checkpoint streams");
        }

        private void UpgradeStreamIfPartitionCheckpoint(string streamId)
        {
            if (streamId.StartsWith("$projections-") && streamId.EndsWith("-checkpoint"))
            {
                var candidates = _existingProjections.Where(v => streamId.StartsWith("$projections-" + v)).ToList();
                if (candidates.Count > 0)
                {
                    if (candidates.Any(v => streamId == "$projections-" + v + "-checkpoint"))
                    {
                        Log("Skipping projection checkpoint stream '{0}'", streamId);
                    }
                    else
                    {
                        UpgradeCheckpointStream(streamId, "Checkpoint");
                    }
                }
                else
                {
                    Log("The '{0}' stream looks like checkpoint stream, but no corresponding projection found", streamId);
                }
            }
        }

        private void UpgradeProjectionCheckpoints()
        {
            foreach (var existingProjection in _existingProjections)
                UpgradeProjectionCheckpoints(existingProjection);
        }

        private void UpgradeProjectionCheckpoints(string existingProjection)
        {
            var checkpointStream = "$projections-" + existingProjection + "-checkpoint";
            UpgradeCheckpointStream(checkpointStream, "ProjectionCheckpoint");
        }

        private void UpgradeCheckpointStream(string checkpointStream, string eventTypeSuffix)
        {
            Log("Reading last event in the projection checkpoint stream '{0}'", checkpointStream);
            var slice = _connection.ReadStreamEventsBackward(checkpointStream, -1, 1, false, _credentials);
            if (slice.Events.Length > 0)
            {
                var lastEvent = slice.Events[0];
                var eventType = lastEvent.Event.EventType;
                Log(
                    "{0} events loaded from the '{1}' stream.  Last event number is: {2} Event type is: {3}",
                    slice.Events.Length, checkpointStream, lastEvent.OriginalEventNumber, eventType);
                if (eventType == eventTypeSuffix)
                {
                    Log("Old style projection checkpoint found at {0}@{1}", lastEvent.OriginalEventNumber, checkpointStream);
                    if (_runUpgrade)
                    {
                        Log("Writing converted checkpoint to stream {0}", checkpointStream);
                        _connection.AppendToStream(
                            checkpointStream, lastEvent.OriginalEventNumber, _credentials,
                            new EventData(
                                Guid.NewGuid(), "$" + eventTypeSuffix, true, lastEvent.Event.Data, lastEvent.Event.Metadata));
                    }
                    else
                    {
                        _upgradeRequired = true;
                        Log("*** Old style projection checkpoint found.  Run with --upgrade option");
                    }
                }
            }
            else
            {
                Log(
                    "No events loaded from the '{0}' stream. Read status: {1}", slice.Events.Length, checkpointStream,
                    slice.Status);
            }
        }

        private void Connect()
        {
            var settings = ConnectionSettings.Create();
            var ip = new IPEndPoint(_ipAddress, _port);
            Log("Connecting to {0}:{1}...", _ipAddress, _port);
            _connection = EventStoreConnection.Create(settings, ip);
	        _connection.Connect();
			_connection.AppendToStream("hello", ExpectedVersion.Any, new EventData(Guid.NewGuid(), "Hello", false, new byte[0], new byte[0]));
			Log("Connected.");
			Log("Username to be used is: {0}", _userName);
			_credentials = new UserCredentials(_userName, _password);
        }

        private void LoadProjectionCatalog()
        {
            Log("Loading projection catalog...");
            StreamEventsSlice projections;
            _lastCatalogEventNumber = 0;
            do
            {
                Log("Reading catalog chunk...");
                projections = _connection.ReadStreamEventsForward("$projections-$all", _lastCatalogEventNumber, 100, false, _credentials);
                Log("{0} events loaded", projections.Events.Length);
                foreach (var e in projections.Events)
                {
                    if (e.Event.EventType == "$stream-created-implicit")
                        continue;
                    var projectionName = Encoding.UTF8.GetString(e.Event.Data);
                    if (e.Event.EventType == ProjectionCreatedOld)
                    {
                        Log("Old style projection registration found for: {0}", projectionName);
                        _oldStyleProjections.Add(projectionName);
                    }
                    else if (e.Event.EventType == ProjectionCreatedNew)
                    {
                        Log("New style projection registration found for: {0}", projectionName);
                        if (_oldStyleProjections.Contains(projectionName))
                        {
                            Log("Old style registration for the {0} is already upgraded", projectionName);
                            _oldStyleProjections.Remove(projectionName);
                        }
                    }
                    else
                    {
                        Log("Skipping event: {0}", e.Event.EventType);
                    }
                    _existingProjections.Add(projectionName);
                    _lastCatalogEventNumber = e.Event.EventNumber;
                }
            } while (!projections.IsEndOfStream);
            Log("Projection catalog loaded.");
        }

        private void UpgradeProjectionCatalog()
        {
            if (!_runUpgrade && _oldStyleProjections.Count > 0)
            {
                Log("*** Skipping projection catalog upgrade.  Run with --upgrade option to upgrade");
                _upgradeRequired = true;
                return;
            }
            Log("Upgrading projection catalog...");
            foreach (string projection in _oldStyleProjections)
            {
                Log("Writing new registration for: {0}", projection);
                _connection.AppendToStream(
                    "$projections-$all", _lastCatalogEventNumber, _credentials,
                    new EventData(Guid.NewGuid(), ProjectionCreatedNew, false, Encoding.UTF8.GetBytes(projection), null));
                _lastCatalogEventNumber++;
                Log("New registration for {0} has been written", projection);
            }
            Log("Projection catalog upgraded");
        }

        private void UpgradeProjectionStreams()
        {
            Log("Upgrading projection definition streams");
            foreach (var projection in _existingProjections)
            {
                var projectionStream = "$projections-" + projection;
                Log("Inspecting projection definition stream {0}", projectionStream);
                UpgradeProjectionStream(projectionStream);
            }
            Log("Projection definition streams upgraded");
        }

        private void UpgradeProjectionStream(string projectionStream)
        {
            Log("Reading the last event from the {0}", projectionStream);
            var lastProjectionVersion = _connection.ReadStreamEventsBackward(
                projectionStream, -1, 1, false, _credentials);
            if (lastProjectionVersion.Events.Length > 0)
            {
                Log("Loaded the last event from the {0} stream", projectionStream);
                var projectionDefinition = lastProjectionVersion.Events[0].Event;
                Log("The last event type is: {0}", projectionDefinition.EventType);
                if (projectionDefinition.EventType == ProjectionUpdatedOld)
                {
                    Log("The old style projection definition event found");
                    if (!_runUpgrade)
                    {
                        Log(
                            "*** Skipping projection definition stream {0} upgrade.  Run with --upgrade option to upgrade",
                            projectionStream);
                        _upgradeRequired = true;
                        return;
                    }
                    Log("Writing new projection definition event to {0}", projectionStream);
                    _connection.AppendToStream(
                        projectionStream, projectionDefinition.EventNumber, _credentials,
                        new EventData(
                            Guid.NewGuid(), ProjectionUpdatedNew, true, projectionDefinition.Data,
                            projectionDefinition.Metadata));
                    Log("New projection definition has been written to {0}", projectionStream);
                }
            }
            else
            {
                Log("The {0} projection definition stream is empty", projectionStream);
            }
        }

        private void Log(string format, params object[] args)
        {
            var message = string.Format(format, args);
            Console.WriteLine(message);
            try
            {
                File.AppendAllText(
                    "projection-upgrade.log", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff - ") + message + "\r\n");
            }
            catch (IOException)
            {
                // ignore errors writing to the log file
            }
        }
    }
}
