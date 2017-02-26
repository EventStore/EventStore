namespace EventStore.Core.Services.Transport.Tcp
{
    public enum TcpCommand: byte
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

        // V1
        WriteEventsV1 = 0x82,
        WriteEventsCompletedV1 = 0x83,

        TransactionStartV1 = 0x84,
        TransactionStartCompletedV1 = 0x85,
        TransactionWriteV1 = 0x86,
        TransactionWriteCompletedV1 = 0x87,
        TransactionCommitV1 = 0x88,
        TransactionCommitCompletedV1 = 0x89,

        DeleteStreamV1 = 0x8A,
        DeleteStreamCompletedV1 = 0x8B,

        ReadEventV1 = 0xB0,
        ReadEventCompletedV1 = 0xB1,
        ReadStreamEventsForwardV1 = 0xB2,
        ReadStreamEventsForwardCompletedV1 = 0xB3,
        ReadStreamEventsBackwardV1 = 0xB4,
        ReadStreamEventsBackwardCompletedV1 = 0xB5,
        ReadAllEventsForwardV1 = 0xB6,
        ReadAllEventsForwardCompletedV1 = 0xB7,
        ReadAllEventsBackwardV1 = 0xB8,
        ReadAllEventsBackwardCompletedV1 = 0xB9,

        SubscribeToStreamV1 = 0xC0,
        SubscriptionConfirmationV1 = 0xC1,
        StreamEventAppearedV1 = 0xC2,
        UnsubscribeFromStreamV1 = 0xC3,
        SubscriptionDroppedV1 = 0xC4,
        ConnectToPersistentSubscriptionV1 = 0xC5,
        PersistentSubscriptionConfirmationV1 = 0xC6,
        PersistentSubscriptionStreamEventAppearedV1 = 0xC7,
        CreatePersistentSubscriptionV1 = 0xC8,
        CreatePersistentSubscriptionCompletedV1 = 0xC9,
        DeletePersistentSubscriptionV1 = 0xCA,
        DeletePersistentSubscriptionCompletedV1 = 0xCB,
        PersistentSubscriptionAckEventsV1 = 0xCC,
        PersistentSubscriptionNakEventsV1 = 0xCD,
        UpdatePersistentSubscriptionV1 = 0xCE,
        UpdatePersistentSubscriptionCompletedV1 = 0xCF,

        // V2
        WriteEvents = 0x92,
        WriteEventsCompleted = 0x93,

        TransactionStart = 0x94,
        TransactionStartCompleted = 0x95,
        TransactionWrite = 0x96,
        TransactionWriteCompleted = 0x97,
        TransactionCommit = 0x98,
        TransactionCommitCompleted = 0x99,

        DeleteStream = 0x9A,
        DeleteStreamCompleted = 0x9B,

        ReadEvent = 0xA0,
        ReadEventCompleted = 0xA1,
        ReadStreamEventsForward = 0xA2,
        ReadStreamEventsForwardCompleted = 0xA3,
        ReadStreamEventsBackward = 0xA4,
        ReadStreamEventsBackwardCompleted = 0xA5,
        ReadAllEventsForward = 0xA6,
        ReadAllEventsForwardCompleted = 0xA7,
        ReadAllEventsBackward = 0xA8,
        ReadAllEventsBackwardCompleted = 0xA9,

        SubscribeToStream = 0xE0,
        SubscriptionConfirmation = 0xE1,
        StreamEventAppeared = 0xE2,
        UnsubscribeFromStream = 0xE3,
        SubscriptionDropped = 0xE4,
        ConnectToPersistentSubscription = 0xE5,
        PersistentSubscriptionConfirmation = 0xE6,
        PersistentSubscriptionStreamEventAppeared = 0xE7,
        CreatePersistentSubscription = 0xE8,
        CreatePersistentSubscriptionCompleted = 0xE9,
        DeletePersistentSubscription = 0xEA,
        DeletePersistentSubscriptionCompleted = 0xEB,
        PersistentSubscriptionAckEvents = 0xEC,
        PersistentSubscriptionNakEvents = 0xED,
        UpdatePersistentSubscription = 0xEE,
        UpdatePersistentSubscriptionCompleted = 0xEF,


        ScavengeDatabase = 0xD0,
        ScavengeDatabaseCompleted = 0xD1,

        BadRequest = 0xF0,
        NotHandled = 0xF1,
        Authenticate = 0xF2,
        Authenticated = 0xF3,
        NotAuthenticated = 0xF4
    }
}