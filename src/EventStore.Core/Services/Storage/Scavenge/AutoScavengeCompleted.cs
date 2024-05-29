using System;
using EventStore.Cluster;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeCompleted(DateTime Date, EndPoint Node);
