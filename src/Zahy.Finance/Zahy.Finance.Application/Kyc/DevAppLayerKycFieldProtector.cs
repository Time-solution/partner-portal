using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Zahy.Finance;

/// <summary>
/// Dev/test app-layer field encryption. Swappable via DI — not a production crypto decision.
/// </summary>
public class DevAppLayerKycFieldProtector : IKycFieldProtector, ITransientDependency
{
    private readonly FinanceKycProtectionOptions _options;

    public DevAppLayerKycFieldProtector(IOptions<FinanceKycProtectionOptions> options)
    {
        _options = options.Value;
    }

    public string Protect(string plaintext)
    {
        Check.NotNull(plaintext, nameof(plaintext));

        var key = ResolveKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length + tag.Length, cipherBytes.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        Check.NotNullOrWhiteSpace(protectedValue, nameof(protectedValue));

        var payload = Convert.FromBase64String(protectedValue);
        var nonce = payload.AsSpan(0, 12).ToArray();
        var tag = payload.AsSpan(12, 16).ToArray();
        var cipherBytes = payload.AsSpan(28).ToArray();
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(ResolveKey(), tagSizeInBytes: 16);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] ResolveKey()
    {
        var material = _options.DevProtectionKeyMaterial;
        if (string.IsNullOrWhiteSpace(material))
        {
            material = "Zahy.Finance.DevKycProtectionKey.ChangeInProduction";
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }
}

public class FinanceKycProtectionOptions
{
    public const string SectionName = "Finance:KycProtection";

    public string? DevProtectionKeyMaterial { get; set; }
}
