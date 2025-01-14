ConnectorsEngine

This allows the user to configure connector pipelines and run them.
It is the core connector logic, without any fancy plugin-loading gubbins.
It can be hosted in a variety of host applications

- The Node (via ConnectorsPlugin, which references this Engine)
- The ConnectedSubsystemHost (also via via ConnectorsPlugin)
- The users own application (which can reference this Engine directly)
