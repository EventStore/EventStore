﻿using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.Jint
{
	[TestFixture]
	public class with_foreach_and_deleted_notification_handled : TestFixtureWithInterpretedProjection {
		protected override void Given() {
			_projection = @"fromAll().foreachStream().when({
                $deleted: function(){}
            })";
			_state = @"{}";
		}

		[Test]
		public void source_definition_is_correct() {
			Assert.AreEqual(true, _source.HandlesDeletedNotifications);
		}
	}
}