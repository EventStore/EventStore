module.exports = [
    {
        title: "Introduction",
        collapsable: true,
        path: "introduction/",
        children: [
            "introduction/",
            "introduction/clients.md",
        ]
    },
    {
        title: "Installation",
        collapsable: true,
        path: "installation/",
        children: [
            "installation/",
            "installation/linux.md",
            "installation/windows.md",
            "installation/docker.md",
            "installation/compatibility.md",
        ]
    },
    {
        title: "Configuration",
        collapsable: true,
        path: "configuration/",
        children: [
            "configuration/",
            "configuration/auto-configured-options.md"
        ]
    },
    {
        title: "Security",
        collapsable: true,
        path: "security/",
        children: [
            "security/",
            "security/configuration.md",
            "security/authentication.md",
            "security/acl.md",
            "security/trusted-intermediary.md",
        ]
    },
    {
        title: "Networking",
        collapsable: true,
        path: "networking/",
        children: [
            "networking/",
            "networking/http.md",
            "networking/tcp.md",
            "networking/nat.md",
            "networking/heartbeat.md",
            "networking/endpoints.md",
        ]
    },
    {
        title: "Clustering",
        collapsable: true,
        path: "clustering/",
        children: [
            "clustering/",
            "clustering/using-dns.md",
            "clustering/using-ip-addresses.md",
            "clustering/gossip.md",
            "clustering/node-roles.md",
            "clustering/acknowledgements.md",
        ]
    },
    {
        title: "Indexes",
        collapsable: true,
        path: "indexes/",
        children: [
            "indexes/",
            "indexes/configuration.md",
            "indexes/tuning.md",
            "indexes/advanced.md",
        ]
    },
    {
        title: "Projections",
        collapsable: true,
        path: "projections/",
        children: [
            "projections/",
            "projections/configuration.md",
            "projections/system-projections.md",
            "projections/user-defined-projections.md",
            "projections/projections-config.md",
            "projections/debugging.md",
        ]
    },
    {
        title: "Operations",
        collapsable: true,
        path: "operations/",
        children: [
            "operations/",
            "operations/scavenge.md",
            "operations/scavenge-options.md",
            "operations/database-backup.md",
        ]
    },
    {
        title: "Diagnostics",
        collapsable: true,
        path: "diagnostics/",
        children: [
            "diagnostics/",
            "diagnostics/logging.md",
            "diagnostics/stats.md",
            "diagnostics/histograms.md",
            "diagnostics/prometheus.md",
            "diagnostics/datadog.md",
        ]
    },
    {
        title: "Event streams",
        collapsable: true,
        children: [
            "streams/metadata-and-reserved-names.md",
            "streams/deleting-streams-and-events.md",
            "streams/system-streams.md"
        ]
    }
]
