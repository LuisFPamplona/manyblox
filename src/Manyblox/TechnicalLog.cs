using System.Text.Json;

namespace Manyblox;

public sealed class TechnicalLog(string root)
{
    private readonly string path = Path.Combine(root, "multi-instance.jsonl");
    private static readonly HashSet<string> Codes =
    [
        "OBJECTS_RESERVED_EXPERIMENTAL", "OBJECTS_RELEASED", "BROWSER_PENDING", "PROTOCOL_REQUESTED",
        "BROWSER_CLOSED_NO_HANDOFF", "HANDOFF_FAILED", "LAUNCH_FAILED", "PROCESS_SNAPSHOT_FAILED",
        "BROWSER_TIMEOUT", "MULTIPLE_CANDIDATES", "PRIOR_CLIENT_ENDED_POSSIBLE_REPLACEMENT",
        "NEW_PROCESS_UNVERIFIED_ACCOUNT", "PRIOR_CLIENT_ENDED_NO_NEW_CLIENT", "NO_NEW_CLIENT_TIMEOUT",
        "CLIENT_ENDED", "PROCESS_STABLE_NOT_GAME_VERIFIED", "ACCOUNT_CONFIRMED_BY_USER_ONLY", "ACTIVATION_FAILED",
        "ACTIVATION_CLIENT_ALREADY_RUNNING", "ACTIVATION_RACE", "ACTIVATION_OBJECT_ALREADY_EXISTS",
        "ACTIVATION_OBJECT_TYPE_CONFLICT", "ACTIVATION_OBJECT_ACCESS_DENIED", "MANAGER_STARTED", "MANAGER_STOPPED",
        "RESERVATION_LOST", "LAUNCH_PREFLIGHT", "CLIENT_BASELINE", "SECOND_LAUNCH_BLOCKED_MULTI_DISABLED",
        "CLIENT_APPEARED", "CLIENT_INITIAL_SNAPSHOT", "CLIENT_DISAPPEARED"
    ];
    public void Write(string code, int? pid)
        => WriteDiagnostic(new InstanceDiagnostic(code, pid, null, null, false, false, 0));

    public void WriteDiagnostic(InstanceDiagnostic value)
    {
        // Restrict to fixed uppercase codes. Do not accept exception messages or session data.
        if (!Codes.Contains(value.Code)) return;
        try
        {
            if (File.Exists(path) && ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > 256 * 1024)) return;
            File.AppendAllText(path, JsonSerializer.Serialize(new
            {
                at = DateTime.UtcNow, managerPid = Environment.ProcessId, code = value.Code,
                pid = value.Pid, startedUtc = value.StartedUtc?.ToUniversalTime(), launchId = value.LaunchId,
                multiEnabled = value.MultiEnabled, objectsHeld = value.ObjectsHeld, clientCount = value.ClientCount
            }) + Environment.NewLine);
        }
        catch { /* Logging must not change process/resource ownership or expose exceptions. */ }
    }
}
