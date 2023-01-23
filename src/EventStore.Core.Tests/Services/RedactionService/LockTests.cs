using System.Threading.Tasks;
using EventStore.Core.Data.Redaction;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.RedactionService {

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class LockTests<TLogFormat, TStreamId> : RedactionServiceTestFixture<TLogFormat,TStreamId> {
		protected override void WriteTestScenario() {}

		private async Task<RedactionMessage.SwitchChunkLockCompleted> TryLock() {
			return await Handle(e =>
					RedactionService.Handle(new RedactionMessage.SwitchChunkLock(e)))
				as RedactionMessage.SwitchChunkLockCompleted;
		}

		private async Task<RedactionMessage.SwitchChunkUnlockCompleted> TryUnlock() {
			return await Handle(e =>
					RedactionService.Handle(new RedactionMessage.SwitchChunkUnlock(e)))
				as RedactionMessage.SwitchChunkUnlockCompleted;
		}

		[Test]
		public async Task can_lock() {
			var msg = await TryLock();
			Assert.AreEqual(SwitchChunkLockResult.Success, msg.Result);
		}

		[Test]
		public async Task can_unlock() {
			await TryLock();
			var msg = await TryUnlock();
			Assert.AreEqual(SwitchChunkUnlockResult.Success, msg.Result);
		}

		[Test]
		public async Task cannot_lock_when_locked() {
			await TryLock();
			var msg = await TryLock();
			Assert.AreEqual(SwitchChunkLockResult.Failed, msg.Result);
		}

		[Test]
		public async Task cannot_unlock_when_unlocked() {
			var msg = await TryUnlock();
			Assert.AreEqual(SwitchChunkUnlockResult.Failed, msg.Result);
		}
	}
}
