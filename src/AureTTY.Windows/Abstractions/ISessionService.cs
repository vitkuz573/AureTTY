using Windows.Win32.System.RemoteDesktop;

namespace AureTTY.Windows.Abstractions;

public interface ISessionService
{
    uint GetActiveConsoleSessionId();

    List<WTS_SESSION_INFOW> GetActiveSessions();

    uint FindTargetSessionId(int targetSessionId);

    uint GetProcessId(uint sessionId, string processName);
}
