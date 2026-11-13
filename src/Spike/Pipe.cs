using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Spike;

/// <summary>
/// The named pipe between the Guard (server, in the service) and the Dogs (clients, one per
/// signed-in user). The Guard only listens to a client that is the same Spike.exe as itself,
/// so a scammer cannot send "my helper is here" from a program of his own.
/// </summary>
internal sealed class PipeServer : IDisposable
{
    internal const string Name = "Spike";

    private readonly List<StreamWriter> _clients = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly string _ownPath;

    /// <summary>A message from a Dog. Raised on a background thread.</summary>
    internal event Action<PipeMessage>? Received;

    /// <summary>A Dog connected (for example after the user signed in).</summary>
    internal event Action? Connected;

    internal PipeServer(string? ownPath = null)
    {
        _ownPath = ownPath ?? Environment.ProcessPath ?? "";
        _ = Task.Run(AcceptLoop);
    }

    internal int ClientCount { get { lock (_clients) return _clients.Count; } }

    internal void Broadcast(PipeMessage message)
    {
        var line = message.ToLine();
        lock (_clients)
        {
            foreach (var w in _clients.ToArray())
            {
                try { w.WriteLine(line); w.Flush(); }
                catch (IOException) { _clients.Remove(w); }
                catch (ObjectDisposedException) { _clients.Remove(w); }
            }
        }
    }

    private static PipeSecurity Security()
    {
        var s = new PipeSecurity();
        s.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        s.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return s;
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = NamedPipeServerStreamAcl.Create(Name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, Security());
                await pipe.WaitForConnectionAsync(_stop.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { await Task.Delay(1000); continue; }

            if (!FromSpike(pipe)) { pipe.Dispose(); continue; }
            _ = Task.Run(() => Serve(pipe));
        }
    }

    private bool FromSpike(NamedPipeServerStream pipe)
    {
        if (!Native.GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out int pid)) return false;
        var path = Native.ProcessPath(pid);
        return path is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(_ownPath), StringComparison.OrdinalIgnoreCase);
    }

    private async Task Serve(NamedPipeServerStream pipe)
    {
        using (pipe)
        {
            var reader = new StreamReader(pipe);
            var writer = new StreamWriter(pipe) { AutoFlush = true };
            lock (_clients) _clients.Add(writer);
            Connected?.Invoke();
            try
            {
                while (!_stop.IsCancellationRequested && pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(_stop.Token);
                    if (line is null) break;
                    if (PipeMessage.FromLine(line) is { } m) Received?.Invoke(m);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
            finally { lock (_clients) _clients.Remove(writer); }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        lock (_clients) _clients.Clear();
    }
}

/// <summary>The Dog's side of the pipe. Keeps reconnecting, so a restarted Guard is found again.</summary>
internal sealed class PipeClient : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private StreamWriter? _writer;

    /// <summary>A message from the Guard. Raised on a background thread.</summary>
    internal event Action<PipeMessage>? Received;

    internal bool IsConnected => _writer is not null;

    internal PipeClient() => _ = Task.Run(Loop);

    internal void Send(PipeMessage message)
    {
        try { _writer?.WriteLine(message.ToLine()); }
        catch (Exception e) when (e is IOException or ObjectDisposedException) { _writer = null; }
    }

    private async Task Loop()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeServer.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000, _stop.Token);
                var reader = new StreamReader(pipe);
                _writer = new StreamWriter(pipe) { AutoFlush = true };
                while (pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(_stop.Token);
                    if (line is null) break;
                    if (PipeMessage.FromLine(line) is { } m) Received?.Invoke(m);
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { return; }
            catch (Exception e) when (e is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException) { }
            _writer = null;
            try { await Task.Delay(2000, _stop.Token); } catch (OperationCanceledException) { return; }
        }
    }

    public void Dispose() => _stop.Cancel();
}
