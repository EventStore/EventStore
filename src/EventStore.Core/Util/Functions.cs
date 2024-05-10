#nullable enable

using System;
using System.Threading.Tasks;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Time;

namespace EventStore.Core.Util;

public static class Functions {
	public static Func<T> Debounce<T>(this Func<T> func, TimeSpan timeout, IClock? clock = null) {
		clock ??= Clock.Instance;
		var seconds = timeout.TotalSeconds;
		Instant lastCalled = default;
		T? current = default;
	
		return () => {
			var now = clock.Now;
			if (lastCalled == default || now.ElapsedSecondsSince(lastCalled) > seconds) {
				current = func();
				lastCalled = now;
			}
	
			return current!;
		};
	}
    
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    public static Func<T> Debounce<T>(this Func<T> getValue, TimeSpan? timeout = default, TimeProvider? time = default) {
        timeout ??= DefaultTimeout;
        time    ??= TimeProvider.System;
        
        var value = default(T);
        var lastRun = 0L;
        
        return () => {
            var now = time.GetTimestamp();
            
            if (lastRun == 0 || time.GetElapsedTime(lastRun) > timeout) {
                value = getValue();
                lastRun = now;
            }

            return value!;
        };
    }
    
    public static Func<Task<T>> Debounce<T>(this Func<Task<T>> getValue, TimeSpan? timeout = default, TimeProvider? time = default) {
        timeout ??= DefaultTimeout;
        time    ??= TimeProvider.System;
        
        var value   = default(T);
        var lastRun = 0L;
        
        return async () => {
            var now = time.GetTimestamp();
            
            if (lastRun == 0 || time.GetElapsedTime(lastRun) > timeout) {
                value   = await getValue().ConfigureAwait(false);
                lastRun = now;
            }

            return value!;
        };
    }
}
