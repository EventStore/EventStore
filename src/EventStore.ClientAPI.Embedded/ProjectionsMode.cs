namespace EventStore.ClientAPI.Embedded
{
    /// <summary>
    /// Enumerates possible modes for running projections
    /// </summary>
    public enum ProjectionsMode
    {
	/// <summary>
	/// Run no projections
	/// </summary>
        None,
	/// <summary>
	/// Run only system projections
	/// </summary>
        System,
	/// <summary>
	/// Run user and system proejctions
	/// </summary>
        All
    }
}