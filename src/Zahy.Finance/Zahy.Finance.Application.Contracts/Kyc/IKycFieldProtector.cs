namespace Zahy.Finance;

/// <summary>
/// Pluggable KYC field protection — dev default uses app-layer encryption;
/// production algorithm/provider is a CTO/security configuration choice.
/// </summary>
public interface IKycFieldProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
