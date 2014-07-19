namespace EventStore.Core.Tests.Services.ElectionsService.Randomized
{
    public static class ElectionParams
    {
		#if CI_TESTS 
			public const int TestRunCount = 10;
		#else
			public const int TestRunCount = 1;
		#endif
        
        public const int MaxIterationCount = 25000;
    }
}