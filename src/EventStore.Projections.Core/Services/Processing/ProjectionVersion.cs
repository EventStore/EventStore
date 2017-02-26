namespace EventStore.Projections.Core.Services.Processing
{
    public struct ProjectionVersion
    {
        public readonly long ProjectionId;
        public readonly long Epoch;
        public readonly long Version;

        public ProjectionVersion(long projectionId, long epoch, long version)
        {
            ProjectionId = projectionId;
            Epoch = epoch;
            Version = version;
        }

        public bool Equals(ProjectionVersion other)
        {
            return ProjectionId == other.ProjectionId && Epoch == other.Epoch && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProjectionVersion && Equals((ProjectionVersion) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(ProjectionId >> 32);
                hashCode = (hashCode*397) ^ (int)(Epoch >> 32);
                hashCode = (hashCode*397) ^ (int)(Version >> 32);
                return hashCode;
            }
        }
    }
}
