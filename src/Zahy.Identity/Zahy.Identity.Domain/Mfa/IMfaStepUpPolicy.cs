using System.Threading.Tasks;

namespace Zahy.Identity.Mfa;

/// <summary>
/// Pluggable seam deciding whether a sensitive action requires an MFA step-up.
/// Off by default; a future implementation can inspect action sensitivity and
/// the principal's AMR/ACR claims.
/// </summary>
public interface IMfaStepUpPolicy
{
    Task<bool> IsStepUpRequiredAsync(string action);
}
