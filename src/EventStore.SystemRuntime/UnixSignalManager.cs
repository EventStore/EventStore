// ReSharper disable CheckNamespace

namespace System.Runtime.InteropServices;

[PublicAPI]
public static class UnixSignalManager {
    // private const int MaxUnixSignals = 64; //max internal limit of UnixSignal instances per process
    
    static readonly List<PosixSignalRegistration> Registrations = [];

    static bool _resetting;
    static bool _suspending;

    public static void OnSignals(PosixSignal[] signals, Action<PosixSignalContext> handler) {
        if (_resetting)
            throw new InvalidOperationException("Cannot subscribe to signals while stopping.");

        foreach (var signal in signals) {
            Registrations.Add(PosixSignalRegistration.Create(signal, ctx => {
                ctx.Cancel = _resetting || _suspending;
                if (!ctx.Cancel) handler(ctx);
            }));
        }
    }
    
    public static void OnSignal(PosixSignal signal, Action<PosixSignalContext> handler) =>
        OnSignals([signal], handler);
    
    public static void OnSignals(PosixSignal[] signals, Func<PosixSignalContext, Task> handler) =>
        OnSignals(signals, ctx => { handler(ctx).GetAwaiter().GetResult(); });
    
    public static void OnSignal(PosixSignal signal, Func<PosixSignalContext, Task> handler) =>
        OnSignals([signal], handler);

    public static void Stop() {
        _resetting = true;

        foreach (var registration in Registrations)
            registration.Dispose();

        Registrations.Clear();
    }
    
    public static void Suspend() => _suspending = true;
    public static void Resume() => _suspending = false;

    public static void OnSIGTERM(Action<PosixSignalContext> handler) => OnSignal(PosixSignal.SIGTERM, handler);
    public static void OnSIGQUIT(Action<PosixSignalContext> handler) => OnSignal(PosixSignal.SIGQUIT, handler);
    public static void OnSIGINT(Action<PosixSignalContext> handler)  => OnSignal(PosixSignal.SIGINT, handler);
    public static void OnSIGHUP(Action<PosixSignalContext> handler)  => OnSignal(PosixSignal.SIGHUP, handler);
    
    public static void OnTermination(Action<PosixSignalContext> handler) => OnSIGTERM(handler);
    public static void OnQuit(Action<PosixSignalContext> handler)        => OnSIGQUIT(handler);
    public static void OnInterrupt(Action<PosixSignalContext> handler)   => OnSIGINT(handler);
    public static void OnHangup(Action<PosixSignalContext> handler)      => OnSIGHUP(handler);
    
    public static void OnSIGTERM(Action handler) => OnSIGTERM(_ => handler());
    public static void OnSIGQUIT(Action handler) => OnSIGQUIT(_ => handler());
    public static void OnSIGINT(Action handler)  => OnSIGINT(_ => handler());
    public static void OnSIGHUP(Action handler)  => OnSIGHUP(_ => handler());
    
    public static void OnTermination(Action handler) => OnSIGTERM(_ => handler());
    public static void OnQuit(Action handler)        => OnSIGQUIT(_ => handler());
    public static void OnInterrupt(Action handler)   => OnSIGINT(_ => handler());
    public static void OnHangup(Action handler)      => OnSIGHUP(_ => handler());
    
    public static void OnSIGTERM(Func<PosixSignalContext, Task> handler) => OnSignal(PosixSignal.SIGTERM, handler);
    public static void OnSIGQUIT(Func<PosixSignalContext, Task> handler) => OnSignal(PosixSignal.SIGQUIT, handler);
    public static void OnSIGINT(Func<PosixSignalContext, Task> handler)  => OnSignal(PosixSignal.SIGINT, handler);
    public static void OnSIGHUP(Func<PosixSignalContext, Task> handler)  => OnSignal(PosixSignal.SIGHUP, handler);
    
    public static void OnTermination(Func<PosixSignalContext, Task> handler) => OnSIGTERM(handler);
    public static void OnQuit(Func<PosixSignalContext, Task> handler)        => OnSIGQUIT(handler);
    public static void OnInterrupt(Func<PosixSignalContext, Task> handler)   => OnSIGINT(handler);
    public static void OnHangup(Func<PosixSignalContext, Task> handler)      => OnSIGHUP(handler);
    
    public static void OnSIGTERM(Func<Task> handler) => OnSIGTERM(_ => handler());
    public static void OnSIGQUIT(Func<Task> handler) => OnSIGQUIT(_ => handler());
    public static void OnSIGINT(Func<Task> handler)  => OnSIGINT(_ => handler());
    public static void OnSIGHUP(Func<Task> handler)  => OnSIGHUP(_ => handler());
    
    public static void OnTermination(Func<Task> handler) => OnSIGTERM(_ => handler());
    public static void OnQuit(Func<Task> handler)        => OnSIGQUIT(_ => handler());
    public static void OnInterrupt(Func<Task> handler)   => OnSIGINT(_ => handler());
    public static void OnHangup(Func<Task> handler)      => OnSIGHUP(_ => handler());
}