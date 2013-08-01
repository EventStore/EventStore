namespace EventStore.ClientAPI
{
    public class StreamAcl
    {
        public readonly string ReadRole;
        public readonly string WriteRole;
        public readonly string DeleteRole;
        public readonly string MetaReadRole;
        public readonly string MetaWriteRole;

        public StreamAcl(string readRole, string writeRole, string deleteRole, string metaReadRole, string metaWriteRole)
        {
            ReadRole = readRole;
            WriteRole = writeRole;
            DeleteRole = deleteRole;
            MetaReadRole = metaReadRole;
            MetaWriteRole = metaWriteRole;
        }

        public override string ToString()
        {
            return string.Format("Read: {0}, Write: {1}, Delete: {2}, MetaRead: {3}, MetaWrite: {4}",
                                 ReadRole, WriteRole, DeleteRole, MetaReadRole, MetaWriteRole);
        }
    }
}