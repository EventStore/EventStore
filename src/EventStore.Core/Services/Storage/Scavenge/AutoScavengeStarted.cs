using System;
using EventStore.Cluster;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeStarted(DateTime Date, EndPoint Node);
