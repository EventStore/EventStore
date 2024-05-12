// ReSharper disable CheckNamespace

using System.Collections.Concurrent;

namespace System.Runtime.InteropServices;

[PublicAPI]
public static class UnixSignalManager {
    static readonly ConcurrentDictionary<PosixSignal, List<PosixSignalRegistration>> Registrations = [];
    
    /// <summary>
    /// Registers a <paramref name="handler" /> that is invoked when one or more <see cref="T:System.Runtime.InteropServices.PosixSignal" />'s occur.
    /// </summary>
    /// <param name="signals">The signals to register for.</param>
    /// <param name="handler">The handler that gets invoked.</param>
    public static void OnSignals(PosixSignal[] signals, Action<PosixSignalContext> handler) {
        foreach (var signal in signals) {
            Registrations.AddOrUpdate<Func<PosixSignalRegistration>>(
                signal,
                static (_, register) => [register()],
                static (_, registrations, register) => {
                    registrations.Add(register());
                    return registrations;
                },
                () => PosixSignalRegistration.Create(signal, handler)
            );
        }
    }
    
    /// <summary>
    /// Registers a <paramref name="handler" /> that is invoked when the <see cref="T:System.Runtime.InteropServices.PosixSignal" /> occurs.
    /// </summary>
    /// <param name="signal">The signal to register for.</param>
    /// <param name="handler">The handler that gets invoked.</param>
    public static void OnSignal(PosixSignal signal, Action<PosixSignalContext> handler) =>
        OnSignals([signal], handler);
    
    /// <summary>
    /// Registers a <paramref name="handler" /> that is invoked when one or more <see cref="T:System.Runtime.InteropServices.PosixSignal" />'s occur.
    /// </summary>
    /// <param name="signals">The signals to register for.</param>
    /// <param name="handler">The handler that gets invoked.</param>
    public static void OnSignals(PosixSignal[] signals, Func<PosixSignalContext, Task> handler) =>
        OnSignals(signals, ctx => { handler(ctx).GetAwaiter().GetResult(); });
    
    /// <summary>
    /// Registers a <paramref name="handler" /> that is invoked when the <see cref="T:System.Runtime.InteropServices.PosixSignal" /> occurs.
    /// </summary>
    /// <param name="signal">The signal to register for.</param>
    /// <param name="handler">The handler that gets invoked.</param>
    public static void OnSignal(PosixSignal signal, Func<PosixSignalContext, Task> handler) =>
        OnSignals([signal], handler);
    
    /// <summary>
    /// Forcefully unregisters all <see cref="T:System.Runtime.InteropServices.PosixSignal" /> registrations
    /// matching the specified <paramref name="signals" />.
    /// <remarks>
    /// By default, it will unregister from all signals.
    /// </remarks>
    /// </summary>
    public static void Unregister(params PosixSignal[] signals) {
        foreach (var signal in signals.Length == 0 ? Enum.GetValues<PosixSignal>() : signals) {
            if (Registrations.TryRemove(signal, out var registrations)) {
                foreach (var registration in registrations)
                    registration.Dispose();
            }
        }
    }

    #region . sintatic sugar .

    /// <summary>
    /// Registers a handler that is invoked when the SIGTERM signal occurs.
    /// SIGTERM is a signal to terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGTERM signal occurs.</param>
    public static void OnSIGTERM(Action<PosixSignalContext> handler) => OnSignal(PosixSignal.SIGTERM, handler);

    /// <summary>
    /// Registers a handler that is invoked when the SIGQUIT signal occurs.
    /// SIGQUIT is a signal to quit the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGQUIT signal occurs.</param>
    public static void OnSIGQUIT(Action<PosixSignalContext> handler) => OnSignal(PosixSignal.SIGQUIT, handler);

    /// <summary>
    /// Registers a handler that is invoked when the SIGINT signal occurs.
    /// SIGINT is a signal to interrupt the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGINT signal occurs.</param>
    public static void OnSIGINT(Action<PosixSignalContext> handler)  => OnSignal(PosixSignal.SIGINT, handler);

    /// <summary>
    /// Registers a handler that is invoked when the SIGHUP signal occurs.
    /// SIGHUP is a signal to hang up or terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGHUP signal occurs.</param>
    public static void OnSIGHUP(Action<PosixSignalContext> handler)  => OnSignal(PosixSignal.SIGHUP, handler);

    /// <summary>
    /// Registers an async handler that is invoked when the SIGTERM signal occurs.
    /// SIGTERM is a signal to terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGTERM signal occurs.</param>
    public static void OnSIGTERM(Func<PosixSignalContext, Task> handler) => OnSignal(PosixSignal.SIGTERM, handler);

    /// <summary>
    /// Registers an async handler that is invoked when the SIGQUIT signal occurs.
    /// SIGQUIT is a signal to quit the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGQUIT signal occurs.</param>
    public static void OnSIGQUIT(Func<PosixSignalContext, Task> handler) => OnSignal(PosixSignal.SIGQUIT, handler);

    /// <summary>
    /// Registers an async handler that is invoked when the SIGINT signal occurs.
    /// SIGINT is a signal to interrupt the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGINT signal occurs.</param>
    public static void OnSIGINT(Func<PosixSignalContext, Task> handler)  => OnSignal(PosixSignal.SIGINT, handler);

    /// <summary>
    /// Registers an async handler that is invoked when the SIGHUP signal occurs.
    /// SIGHUP is a signal to hang up or terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGHUP signal occurs.</param>
    public static void OnSIGHUP(Func<PosixSignalContext, Task> handler)  => OnSignal(PosixSignal.SIGHUP, handler);

    /// <summary>
    /// Registers a handler that is invoked when the SIGTERM signal occurs.
    /// SIGTERM is a signal to terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGTERM signal occurs.</param>
    public static void OnSIGTERM(Action handler) => OnSIGTERM(_ => handler());

    /// <summary>
    /// Registers a handler that is invoked when the SIGQUIT signal occurs.
    /// SIGQUIT is a signal to quit the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGQUIT signal occurs.</param>
    public static void OnSIGQUIT(Action handler) => OnSIGQUIT(_ => handler());

    /// <summary>
    /// Registers a handler that is invoked when the SIGINT signal occurs.
    /// SIGINT is a signal to interrupt the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGINT signal occurs.</param>
    public static void OnSIGINT(Action handler)  => OnSIGINT(_ => handler());

    /// <summary>
    /// Registers a handler that is invoked when the SIGHUP signal occurs.
    /// SIGHUP is a signal to hang up or terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGHUP signal occurs.</param>
    public static void OnSIGHUP(Action handler)  => OnSIGHUP(_ => handler());

    /// <summary>
    /// Registers an async handler that is invoked when the SIGTERM signal occurs.
    /// SIGTERM is a signal to terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGTERM signal occurs.</param>
    public static void OnSIGTERM(Func<Task> handler) => OnSIGTERM(_ => handler());

    /// <summary>
    /// Registers an async handler that is invoked when the SIGQUIT signal occurs.
    /// SIGQUIT is a signal to quit the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGQUIT signal occurs.</param>
    public static void OnSIGQUIT(Func<Task> handler) => OnSIGQUIT(_ => handler());

    /// <summary>
    /// Registers an async handler that is invoked when the SIGINT signal occurs.
    /// SIGINT is a signal to interrupt the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGINT signal occurs.</param>
    public static void OnSIGINT(Func<Task> handler)  => OnSIGINT(_ => handler());

    /// <summary>
    /// Registers an async handler that is invoked when the SIGHUP signal occurs.
    /// SIGHUP is a signal to hang up or terminate the process, it can be caught and interpreted or ignored by the process.
    /// </summary>
    /// <param name="handler">The handler that gets invoked when the SIGHUP signal occurs.</param>
    public static void OnSIGHUP(Func<Task> handler)  => OnSIGHUP(_ => handler());

    #endregion
}