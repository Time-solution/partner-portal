namespace Zahy.Identity.Account;

public class LoginResultDto
{
    public bool Success { get; set; }

    /// <summary>True when MFA step-up is required before the session is granted.</summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>Stable error code (e.g. "InvalidCredentials", "LockedOut"); null on success.</summary>
    public string? Error { get; set; }
}
