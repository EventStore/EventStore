﻿using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Tests;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_executing_query.with_long_from_all_query {
	[TestFixture]
	public class when_getting_result_and_timeout_exceeded : specification_with_standard_projections_runnning {
		protected override async Task Given() {
			await base.Given();

			await PostEvent("stream-1", "type1", "{}");
			await PostEvent("stream-1", "type1", "{}");
			await PostEvent("stream-1", "type1", "{}");

			WaitIdle();
		}

		[Test, Category("Network"), Timeout(60000)]
		public async Task throws_exception() {
			const string query = @"
fromAll().when({
    $init: function(){return {count:0}},
    type1: function(s,e){
        var start = new Date();
        while(new Date()-start < 1e7){}
        
        s.count++;
    },
});
";
			await AssertEx.ThrowsAsync<OperationTimedOutException>(() => _queryManager.ExecuteAsync("query", query,
				TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(5000), _admin));
		}
	}
}
