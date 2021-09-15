module.exports = [
    {
        title: "Introduction",
        collapsable: true,
        path: "introduction/",
        children: [
            "introduction/",
            "introduction/clients.md"
        ]
    },
    {
        title: "Installation",
        path: "installation/",
        collapsable: true,
        children: [
            "installation/",
            "installation/linux.md",
            "installation/docker.md",
            "installation/windows.md",
            "installation/macos.md",
            // "installation/kubernetes.md",
            "installation/configuration.md"
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
            // "clustering/cluster-without-manager-nodes.md",
            // "clustering/cluster-with-manager-nodes.md",
            "clustering/node-roles.md",
            "clustering/acknowledgements.md",
        ]
    },
    {
        title: "Networking",
        collapsable: true,
        path: "networking/",
        children: [
            "networking/",
            "networking/external.md",
            "networking/internal.md",
            "networking/nat.md",
            "networking/heartbeat.md",
            "networking/endpoints.md",
        ]
    },
    {
        title: "Security",
        collapsable: true,
        path: "security/",
        children: [
            "security/",
            "security/authentication.md",
            "security/trusted-intermediary.md",
            "security/acl.md",
            "security/ssl-linux.md",
            "security/ssl-windows.md",
            "security/ssl-docker.md",
            "security/configuration.md"
        ]
    },
    {
        title: "Admin UI",
        collapsable: true,
        path: "admin-ui/",
        children: [
            "admin-ui/"
        ]
    },
    {
        title: "Server",
        collapsable: true,
        path: "server/",
        children: [
            "server/",
            "server/default-directories.md",
            "server/database.md",
            "server/threading.md",
            "server/caching.md",
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
        title: "Event streams",
        collapsable: true,
        path: "streams/",
        children: [
            "streams/",
            "streams/metadata-and-reserved-names.md",
            "streams/deleting-streams-and-events.md",
            "streams/system-streams.md"
        ]
    }
]
