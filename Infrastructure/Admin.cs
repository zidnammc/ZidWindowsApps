using System.Security.Principal;

namespace ZidWindowsApps;

public static class Admin
{
    private static bool? _cached;

    public static bool IsAdministrator
    {
        get
        {
            _cached ??= Check();
            return _cached.Value;
        }
    }

    private static bool Check()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
