using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using System.Threading.Tasks;

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
        private long _lastTfPosition;
        private double _oldTfScanPercent;

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

        private void Connect()
        {
            var settings = ConnectionSettings.Create();
            var ip = new IPEndPoint(_ipAddress, _port);
            Log("Connecting to {0}:{1}...", _ipAddress, _port);
            _connection = EventStoreConnection.Create(settings, ip);
            _connection.ConnectAsync();
            _connection.AppendToStreamAsync("hello", ExpectedVersion.Any, new EventData(Guid.NewGuid(), "Hello", false, new byte[0], new byte[0]));
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
                projections = _connection.ReadStreamEventsForwardAsync("$projections-$all", _lastCatalogEventNumber, 100, false, _credentials).Result;
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
                _connection.AppendToStreamAsync(
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
            var lastProjectionVersion = _connection.ReadStreamEventsBackwardAsync(
                projectionStream, -1, 1, false, _credentials).Result;
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
                    _connection.AppendToStreamAsync(
                        projectionStream, projectionDefinition.EventNumber, _credentials,
                        new EventData(
                            Guid.NewGuid(), ProjectionUpdatedNew, true, projectionDefinition.Data,
                            projectionDefinition.Metadata)).Wait();
                    Log("New projection definition has been written to {0}", projectionStream);
                }
            }
            else
            {
                Log("The {0} projection definition stream is empty", projectionStream);
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

        private void UpgradeProjectionPartitionCheckpoints()
        {
            Log("Looking for projection partition checkpoint streams");
            var from = Position.Start;
            _oldTfScanPercent = 0d;
            var lastSlice = _connection.ReadAllEventsBackwardAsync(Position.End, 1, false, _credentials).Result;
            if (lastSlice.Events.Length == 0)
                throw new Exception("Empty TF");
            _lastTfPosition = lastSlice.Events[0].OriginalPosition.Value.PreparePosition;
            AllEventsSlice slice;
            do
            {
                slice = _connection.ReadAllEventsForwardAsync(@from, 100, false, _credentials).Result;
                DisplayTfScanProgress(slice);
                foreach (var @event in slice.Events)
                    if (@event.OriginalEventNumber == 0)
                        UpgradeStreamIfPartitionCheckpoint(@event.OriginalStreamId);
                from = slice.NextPosition;
            } while (!slice.IsEndOfStream);
            Log("Completed looking for partition checkpoint streams");
        }

        private void DisplayTfScanProgress(AllEventsSlice slice)
        {
            if (slice.Events.Length > 0)
            {
                var percent =
                    Math.Round(
                        (float) slice.Events[slice.Events.Length - 1].OriginalPosition.Value.PreparePosition/_lastTfPosition*100);
                if (percent != _oldTfScanPercent)
                {
                    _oldTfScanPercent = percent;
                    Console.WriteLine("{0,2}% completed.", percent);
                }
            }
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

        private void UpgradeCheckpointStream(string checkpointStream, string eventTypeSuffix)
        {
            Log("Reading last event in the projection checkpoint stream '{0}'", checkpointStream);
            var slice = _connection.ReadStreamEventsBackwardAsync(checkpointStream, -1, 1, false, _credentials).Result;
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
                        _connection.AppendToStreamAsync(
                            checkpointStream, lastEvent.OriginalEventNumber, _credentials,
                            new EventData(
                                Guid.NewGuid(), "$" + eventTypeSuffix, true, lastEvent.Event.Data, lastEvent.Event.Metadata)).Wait();
;
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
