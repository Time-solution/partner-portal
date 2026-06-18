namespace Zahy.Identity.Mfa;

/// <summary>
/// Multi-factor / step-up options. Disabled by default (IAM-6); when enabled,
/// the step-up policy can require a recent strong-auth (AMR/ACR) for sensitive
/// actions. Bound from configuration section "Zahy:Mfa".
/// </summary>
public class ZahyMfaOptions
{
    public bool Enabled { get; set; }
}
