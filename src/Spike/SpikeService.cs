using System.ServiceProcess;

namespace Spike;

/// <summary>The Windows service "Spike" (LocalSystem). Hosts the <see cref="Guard"/>.</summary>
internal sealed class SpikeService : ServiceBase
{
    internal const string Name = "Spike";

    private Guard? _guard;

    internal SpikeService()
    {
        ServiceName = Name;
        CanStop = true;
        CanShutdown = true;
        CanHandleSessionChangeEvent = true;
    }

    protected override void OnStart(string[] args)
    {
        _guard = new Guard(startDogs: true);
        _guard.Start();
    }

    protected override void OnStop()
    {
        _guard?.Stopping();
        _guard?.Dispose();
        _guard = null;
    }

    protected override void OnShutdown()
    {
        // Windows shuts down: no warning needed.
        _guard?.Dispose();
        _guard = null;
    }
}
