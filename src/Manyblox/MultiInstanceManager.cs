using System.Diagnostics;

namespace Manyblox;

public sealed record ClientProcess(int Pid, DateTime Started, string Title);
public sealed record InstanceDiagnostic(string Code, int? Pid, DateTime? StartedUtc, Guid? LaunchId,
    bool MultiEnabled, bool ObjectsHeld, int ClientCount);
public enum LaunchPhase { PreparingBrowser, AwaitingProcess, ProcessObserved, StableProcess, Ended, Failed, TimedOut, Ambiguous }
public sealed class LaunchObservation
{
    public Guid Id { get; } = Guid.NewGuid();
    public string RequestedAccount { get; init; } = "";
    public LaunchPhase Phase { get; internal set; } = LaunchPhase.PreparingBrowser;
    public ClientProcess? Client { get; internal set; }
    public bool AccountConfirmedByUser { get; internal set; }
    public string Code { get; internal set; } = "BROWSER_PENDING";
    internal HashSet<(int, DateTime)> Baseline { get; set; } = [];
    internal DateTime RequestedAt { get; set; }
    internal DateTime ObservedAt { get; set; }
}

// Preparation references UpdateMultiRoblox in Roblox Account Manager (ic3w0lf22),
// Copyright © ic3 2023, GPL v3. This adaptation retains a dedicated owner thread,
// refuses preexisting objects and explicitly releases only resources we created.
// Only creates our own synchronization objects. Never opens or closes Roblox handles.
// Mutex ownership is thread-affine: creation, ownership and release stay on one thread.
public sealed class NamedObjectReservation : IDisposable
{
    private readonly ManualResetEventSlim stop = new(false);
    private readonly Thread worker;
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed;
    private volatile bool held;
    public Task Ready => completion.Task;
    public bool IsHeld => held && worker.IsAlive;

    public NamedObjectReservation(IReadOnlyList<string> names)
    {
        if (names.Count != 2 || names.Distinct(StringComparer.Ordinal).Count() != 2 ||
            names.Any(n => n.Length is < 1 or > 150 || n.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))))
            throw new ArgumentException("Invalid synchronization names.");
        string[] copy = names.ToArray();
        worker = new Thread(() => Hold(copy)) { IsBackground = true, Name = "Manyblox synchronization owner" };
        worker.Start();
    }

    private void Hold(string[] names)
    {
        var owned = new List<Mutex>();
        try
        {
            foreach (string name in names)
            {
                var mutex = new Mutex(true, name, out bool created);
                if (!created)
                {
                    mutex.Dispose();
                    throw new InvalidOperationException("OBJECT_ALREADY_EXISTS");
                }
                owned.Add(mutex);
            }
            held = true;
            completion.SetResult();
            stop.Wait();
        }
        catch (UnauthorizedAccessException) { completion.TrySetException(new InvalidOperationException("OBJECT_ACCESS_DENIED")); }
        catch (WaitHandleCannotBeOpenedException) { completion.TrySetException(new InvalidOperationException("OBJECT_TYPE_CONFLICT")); }
        catch (InvalidOperationException e) when (e.Message == "OBJECT_ALREADY_EXISTS") { completion.TrySetException(new InvalidOperationException("OBJECT_ALREADY_EXISTS")); }
        catch (Exception) { completion.TrySetException(new InvalidOperationException("OBJECT_RESERVATION_FAILED")); }
        finally
        {
            held = false;
            foreach (var mutex in owned.AsEnumerable().Reverse())
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stop.Set();
        worker.Join(); // No network/process waits on this thread; only two local objects.
        stop.Dispose();
    }
}

public sealed class MultiInstanceManager : IDisposable
{
    private static readonly string[] Names = ["ROBLOX_singletonMutex", "ROBLOX_singletonEvent"];
    private readonly Func<IReadOnlyList<ClientProcess>> snapshot;
    private readonly Func<bool> launchersActive;
    private readonly Func<DateTime> clock;
    private readonly Action<string, int?> log;
    private readonly Action<InstanceDiagnostic> diagnostic;
    private readonly Func<NamedObjectReservation> createReservation;
    private readonly List<LaunchObservation> launches = [];
    private NamedObjectReservation? reservation;
    private bool disposed;
    private bool enabling;
    private Dictionary<(int, DateTime), ClientProcess> previousClients = [];
    private bool hasSnapshot;
    public bool Enabled => reservation?.IsHeld == true;
    public bool MonitoringAvailable { get; private set; } = true;
    public IReadOnlyList<ClientProcess> Clients { get; private set; } = [];
    public IReadOnlyList<LaunchObservation> Launches => launches.AsReadOnly();

    public MultiInstanceManager(Func<IReadOnlyList<ClientProcess>>? snapshot = null,
        Func<DateTime>? clock = null, Action<string, int?>? log = null, Func<bool>? launchersActive = null,
        Func<NamedObjectReservation>? createReservation = null, Action<InstanceDiagnostic>? diagnostic = null)
    {
        this.snapshot = snapshot ?? ReadClients;
        this.clock = clock ?? (() => DateTime.UtcNow);
        this.log = log ?? ((_, _) => { });
        this.launchersActive = launchersActive ?? (() => AnyProcess("RobloxPlayerLauncher") || AnyProcess("RobloxPlayerInstaller"));
        this.createReservation = createReservation ?? (() => new NamedObjectReservation(Names));
        this.diagnostic = diagnostic ?? (_ => { });
        Emit("MANAGER_STARTED");
    }

    private static bool AnyProcess(string name)
    {
        var processes = Process.GetProcessesByName(name);
        try { return processes.Any(p => !p.HasExited); }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    private static IReadOnlyList<ClientProcess> ReadClients()
    {
        var result = new List<ClientProcess>();
        var processes = Process.GetProcessesByName("RobloxPlayerBeta");
        try
        {
            foreach (var p in processes)
            {
                try { if (!p.HasExited) result.Add(new(p.Id, p.StartTime.ToUniversalTime(), p.MainWindowTitle)); }
                catch (InvalidOperationException) { } // The process exited while taking the snapshot.
            }
        }
        finally { foreach (var p in processes) p.Dispose(); }
        return result;
    }

    public async Task EnableAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Enabled) return;
        if (enabling || HasPendingLaunch) throw new InvalidOperationException("LAUNCH_PENDING");
        enabling = true;
        NamedObjectReservation? candidate = null;
        try
        {
            var current = snapshot();
            if (current.Count > 0 || launchersActive()) throw new InvalidOperationException("CLIENT_ALREADY_RUNNING");
            candidate = createReservation();
            await candidate.Ready;
            // Fail closed if an outside launch raced activation. No handles are reclaimed.
            if (disposed || snapshot().Count > 0 || launchersActive()) throw new InvalidOperationException("ACTIVATION_RACE");
            if (!candidate.IsHeld) throw new InvalidOperationException("RESERVATION_LOST");
            reservation = candidate;
            Emit("OBJECTS_RESERVED_EXPERIMENTAL");
        }
        catch (Exception e)
        {
            candidate?.Dispose();
            string code = e is InvalidOperationException ? e.Message switch
            {
                "CLIENT_ALREADY_RUNNING" => "ACTIVATION_CLIENT_ALREADY_RUNNING",
                "ACTIVATION_RACE" => "ACTIVATION_RACE",
                "OBJECT_ALREADY_EXISTS" => "ACTIVATION_OBJECT_ALREADY_EXISTS",
                "OBJECT_TYPE_CONFLICT" => "ACTIVATION_OBJECT_TYPE_CONFLICT",
                "OBJECT_ACCESS_DENIED" => "ACTIVATION_OBJECT_ACCESS_DENIED",
                _ => "ACTIVATION_FAILED"
            } : "ACTIVATION_FAILED";
            Emit(code);
            throw;
        }
        finally { enabling = false; }
    }

    public void Disable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (snapshot().Count > 0 || launchersActive() || HasPendingLaunch)
            throw new InvalidOperationException("CLIENTS_OR_LAUNCHES_ACTIVE");
        reservation?.Dispose();
        reservation = null;
        Emit("OBJECTS_RELEASED");
    }

    public void Preflight(LaunchObservation? launch = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (enabling) throw new InvalidOperationException("ACTIVATION_PENDING");
        if (reservation is not null && !reservation.IsHeld)
        {
            Emit("RESERVATION_LOST");
            throw new InvalidOperationException("RESERVATION_LOST");
        }
        Poll();
        if (!MonitoringAvailable) throw new InvalidOperationException("PROCESS_SNAPSHOT_FAILED");
        Emit("LAUNCH_PREFLIGHT", launch: launch);
        foreach (var client in Clients) Emit("CLIENT_BASELINE", client, launch);
        if (!Enabled && Clients.Count > 0)
        {
            Emit("SECOND_LAUNCH_BLOCKED_MULTI_DISABLED");
            throw new InvalidOperationException("MULTI_NOT_PREPARED");
        }
    }

    public bool HasPendingLaunch => launches.Any(l => l.Phase is LaunchPhase.PreparingBrowser or LaunchPhase.AwaitingProcess or LaunchPhase.ProcessObserved);
    public LaunchObservation BeginLaunch(string account)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (HasPendingLaunch || enabling) throw new InvalidOperationException("LAUNCH_PENDING");
        if (account.Length is < 3 or > 20 || account.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new InvalidDataException("Invalid account label.");
        Preflight();
        if (launches.Count >= 100) launches.RemoveAt(0);
        var launch = new LaunchObservation { RequestedAccount = account, RequestedAt = clock() };
        launches.Add(launch);
        Emit("BROWSER_PENDING", launch: launch);
        return launch;
    }

    public void BeforeHandoff(LaunchObservation launch)
    {
        if (!launches.Contains(launch) || launch.Phase != LaunchPhase.PreparingBrowser || disposed)
            throw new InvalidOperationException("INVALID_LAUNCH_STATE");
        Preflight(launch); // Repeat immediately before forwarding; an outside client may have appeared.
        launch.Baseline = Clients.Select(p => (p.Pid, p.Started)).ToHashSet();
        launch.RequestedAt = clock();
        launch.Phase = LaunchPhase.AwaitingProcess;
        launch.Code = "PROTOCOL_REQUESTED";
        Emit(launch.Code, launch: launch);
    }

    public void BrowserClosed(LaunchObservation launch)
    {
        if (launch.Phase == LaunchPhase.PreparingBrowser) Fail(launch, "BROWSER_CLOSED_NO_HANDOFF");
    }
    public void Fail(LaunchObservation launch, string code)
    {
        // Only fixed codes from our call sites; no exception, URI or credential text.
        if (code is not ("BROWSER_CLOSED_NO_HANDOFF" or "HANDOFF_FAILED")) code = "LAUNCH_FAILED";
        launch.Phase = LaunchPhase.Failed;
        launch.Code = code;
        launch.AccountConfirmedByUser = false;
        Emit(code, launch.Client, launch);
    }

    public void Poll()
    {
        if (disposed) return;
        try { Clients = snapshot(); MonitoringAvailable = true; }
        catch { MonitoringAvailable = false; Emit("PROCESS_SNAPSHOT_FAILED"); return; }
        var now = clock();
        var currentClients = Clients.ToDictionary(p => (p.Pid, p.Started));
        foreach (var entry in currentClients)
            if (!previousClients.ContainsKey(entry.Key)) Emit(hasSnapshot ? "CLIENT_APPEARED" : "CLIENT_INITIAL_SNAPSHOT", entry.Value);
        foreach (var entry in previousClients)
            if (!currentClients.ContainsKey(entry.Key)) Emit("CLIENT_DISAPPEARED", entry.Value);
        previousClients = currentClients;
        hasSnapshot = true;
        var live = Clients.Select(p => (p.Pid, p.Started)).ToHashSet();
        var claimed = launches.Where(l => l.Client is not null).Select(l => (l.Client!.Pid, l.Client.Started)).ToHashSet();
        foreach (var launch in launches)
        {
            if (launch.Phase == LaunchPhase.PreparingBrowser && now - launch.RequestedAt > TimeSpan.FromMinutes(3))
                SetPhase(launch, LaunchPhase.TimedOut, "BROWSER_TIMEOUT");
            if (launch.Phase == LaunchPhase.AwaitingProcess)
            {
                var candidates = Clients.Where(p => !launch.Baseline.Contains((p.Pid, p.Started)) && !claimed.Contains((p.Pid, p.Started))).ToArray();
                bool priorEnded = launch.Baseline.Any(p => !live.Contains(p));
                if (candidates.Length > 1) SetPhase(launch, LaunchPhase.Ambiguous, "MULTIPLE_CANDIDATES");
                else if (candidates.Length == 1)
                {
                    launch.Client = candidates[0];
                    claimed.Add((launch.Client.Pid, launch.Client.Started));
                    launch.ObservedAt = now;
                    SetPhase(launch, LaunchPhase.ProcessObserved, priorEnded ? "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT" : "NEW_PROCESS_UNVERIFIED_ACCOUNT");
                }
                else if (now - launch.RequestedAt > TimeSpan.FromSeconds(90)) SetPhase(launch, LaunchPhase.TimedOut, priorEnded ? "PRIOR_CLIENT_ENDED_NO_NEW_CLIENT" : "NO_NEW_CLIENT_TIMEOUT");
            }
            if (launch.Phase is LaunchPhase.ProcessObserved or LaunchPhase.StableProcess)
            {
                if (launch.Baseline.Any(p => !live.Contains(p)) && launch.Code != "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT")
                    SetPhase(launch, launch.Phase, "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT");
                if (launch.Client is null || !live.Contains((launch.Client.Pid, launch.Client.Started)))
                {
                    launch.AccountConfirmedByUser = false;
                    SetPhase(launch, LaunchPhase.Ended, "CLIENT_ENDED");
                }
                else if (launch.Phase == LaunchPhase.ProcessObserved && now - launch.ObservedAt >= TimeSpan.FromSeconds(10))
                    SetPhase(launch, LaunchPhase.StableProcess, launch.Code == "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT" ? launch.Code : "PROCESS_STABLE_NOT_GAME_VERIFIED");
            }
        }
    }

    private void SetPhase(LaunchObservation launch, LaunchPhase phase, string code)
    {
        launch.Phase = phase;
        launch.Code = code;
        Emit(code, launch.Client, launch);
    }

    private void Emit(string code, ClientProcess? client = null, LaunchObservation? launch = null)
    {
        log(code, client?.Pid);
        diagnostic(new InstanceDiagnostic(code, client?.Pid, client?.Started, launch?.Id,
            Enabled, reservation?.IsHeld == true, Clients.Count));
    }

    public void ConfirmAccountByUser(LaunchObservation launch)
    {
        Poll();
        if (!MonitoringAvailable || launch.Phase != LaunchPhase.StableProcess || launch.Client is null ||
            !Clients.Any(p => p.Pid == launch.Client.Pid && p.Started == launch.Client.Started))
            throw new InvalidOperationException("CLIENT_NOT_STABLE");
        launch.AccountConfirmedByUser = true;
        Emit("ACCOUNT_CONFIRMED_BY_USER_ONLY", launch.Client, launch);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        reservation?.Dispose();
        reservation = null;
        Emit("MANAGER_STOPPED");
    }
}
