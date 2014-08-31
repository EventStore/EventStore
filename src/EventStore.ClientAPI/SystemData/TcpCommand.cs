namespace EventStore.ClientAPI.SystemData
{
    internal enum TcpCommand: byte
    {
        HeartbeatRequestCommand = 0x01,
        HeartbeatResponseCommand = 0x02,

        Ping = 0x03,
        Pong = 0x04,

        PrepareAck = 0x05,
        CommitAck = 0x06,

        SlaveAssignment = 0x07,
        CloneAssignment = 0x08,

        SubscribeReplica = 0x10,
        ReplicaLogPositionAck = 0x11,
        CreateChunk = 0x12,
        RawChunkBulk = 0x13,
        DataChunkBulk = 0x14,
        ReplicaSubscriptionRetry = 0x15,
        ReplicaSubscribed = 0x16,

        // CLIENT COMMANDS
//        CreateStream = 0x80,
//        CreateStreamCompleted = 0x81,

        WriteEvents = 0x82,
        WriteEventsCompleted = 0x83,

        TransactionStart = 0x84,
        TransactionStartCompleted = 0x85,
        TransactionWrite = 0x86,
        TransactionWriteCompleted = 0x87,
        TransactionCommit = 0x88,
        TransactionCommitCompleted = 0x89,

        DeleteStream = 0x8A,
        DeleteStreamCompleted = 0x8B,

        ReadEvent = 0xB0,
        ReadEventCompleted = 0xB1,
        ReadStreamEventsForward = 0xB2,
        ReadStreamEventsForwardCompleted = 0xB3,
        ReadStreamEventsBackward = 0xB4,
        ReadStreamEventsBackwardCompleted = 0xB5,
        ReadAllEventsForward = 0xB6,
        ReadAllEventsForwardCompleted = 0xB7,
        ReadAllEventsBackward = 0xB8,
        ReadAllEventsBackwardCompleted = 0xB9,

        SubscribeToStream = 0xC0,
        SubscriptionConfirmation = 0xC1,
        StreamEventAppeared = 0xC2,
        UnsubscribeFromStream = 0xC3,
        SubscriptionDropped = 0xC4,
        ConnectToPersistentSubscription = 0xC5,
        PersistentSubscriptionConfirmation = 0xC6,
        PersistentSubscriptionStreamEventAppeared = 0xC7,
        CreatePersistentSubscription = 0xC8,
        CreatePersistentSubscriptionCompleted = 0xC9,
        DeletePersistentSubscription = 0xCA,
        DeletePersistentSubscriptionCompleted = 0xCB,
        PersistentSubscriptionAckEvents = 0xCC,
        PersistentSubscriptionNakEvents = 0xCD,

        ScavengeDatabase = 0xD0,
        ScavengeDatabaseCompleted = 0xD1,

        BadRequest = 0xF0,
        NotHandled = 0xF1,
        Authenticate = 0xF2,
        Authenticated = 0xF3,
        NotAuthenticated = 0xF4
   }
}