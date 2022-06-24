using System;
using EventStore.Core.Helpers;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Helpers {
	class TestObjectParams : IReusableObjectInitParams {
		public int Value;

		public TestObjectParams(int value) {
			Value = value;
		}
	}

	class TestObject : IReusableObject {
		public int Value;
		public bool WasReset;
		public void Initialize(IReusableObjectInitParams initParams) {
			Value = ((TestObjectParams)initParams).Value;
		}

		public void Reset() {
			Value = -1;
			WasReset = true;
		}
	}

	public class ReusableObjectTests {
		private readonly ReusableObject<TestObject> _reusableObject;

		public ReusableObjectTests() {
			_reusableObject = ReusableObject.Create(new TestObject());
		}

		[Fact]
		public void can_acquire_object() {
			var obj = _reusableObject.Acquire(new TestObjectParams(5));
			Assert.Equal(5, obj.Value);
		}

		[Fact]
		public void can_release_object() {
			_reusableObject.Acquire(new TestObjectParams(5));
			_reusableObject.Release();
		}

		[Fact]
		public void object_is_reset_when_acquired() {
			var obj = _reusableObject.Acquire(new TestObjectParams(5));
			Assert.True(obj.WasReset);
		}

		[Fact]
		public void can_reacquire_object_if_released() {
			_reusableObject.Acquire(new TestObjectParams(3));
			_reusableObject.Release();
			var obj = _reusableObject.Acquire(new TestObjectParams(6));
			Assert.Equal(6, obj.Value);
		}

		[Fact]
		public void cannot_acquire_object_if_not_released() {
			_reusableObject.Acquire(new TestObjectParams(3));
			Assert.Throws<InvalidOperationException>(() => _reusableObject.Acquire(new TestObjectParams(3)));
		}

		[Fact]
		public void cannot_release_object_if_not_acquired() {
			Assert.Throws<InvalidOperationException>(() => _reusableObject.Release());
		}
	}
}
