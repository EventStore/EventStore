ConnectedSubsystemHost

This executable loads the plugins of type ISubsystemsPlugin<IClient>, just like the Node does
Unlike the Node, the IClient that we provide to the subsystems wraps the grpc client.

Immediately this is for running the ConnectorsEngine externally, it's possible/likely we will
want to be able to run other things this way, such as PersistentSubscriptions, Projections,
perhaps almost anything that currently uses an IODispatcher. But if not, we might not need
this project in the end.
