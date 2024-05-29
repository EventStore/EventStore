using System;
using EventStore.Cluster;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeNodeDesignated(DateTime Date, EndPoint Node);
