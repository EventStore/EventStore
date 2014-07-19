namespace EventStore.Projections.Core.Services.Processing
{
    public struct ProjectionVersion
    {
        public readonly int ProjectionId;
        public readonly int Epoch;
        public readonly int Version;

        public ProjectionVersion(int projectionId, int epoch, int version)
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
                var hashCode = ProjectionId;
                hashCode = (hashCode*397) ^ Epoch;
                hashCode = (hashCode*397) ^ Version;
                return hashCode;
            }
        }
    }
}
