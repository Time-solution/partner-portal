using System.Threading.Tasks;

namespace Zahy.Identity.Auditing;

/// <summary>Records consequential admin actions to the admin audit log.</summary>
public interface IAdminAuditLogger
{
    Task LogAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string result = AdminAuditResults.Success,
        string? extraData = null);
}

public static class AdminAuditResults
{
    public const string Success = "Success";
    public const string Denied = "Denied";
    public const string Failed = "Failed";
}
