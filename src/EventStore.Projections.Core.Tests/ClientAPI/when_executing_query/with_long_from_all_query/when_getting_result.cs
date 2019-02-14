using System;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.query_result.with_long_from_all_query {
	[TestFixture]
	public class when_getting_result : specification_with_standard_projections_runnning {
		protected override void Given() {
			base.Given();

			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-1", "type1", "{}");

			WaitIdle();
		}

		[Test, Category("Network")]
		public void waits_for_results() {
			const string query = @"
fromAll().when({
    $init: function(){return {count:0}},
    type1: function(s,e){
        var start = new Date();
        while(new Date()-start < 500){}
        
        s.count++;
    },
});
";

			var result = _queryManager
				.ExecuteAsync("query", query, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(5000), _admin)
				.GetAwaiter().GetResult();
			Assert.AreEqual("{\"count\":3}", result);
		}
	}
}
