using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Zahy.Hosting;

/// <summary>
/// Ensures every non-Development instance loads the same OpenIddict signing/encryption certificate from config.
/// </summary>
public static class OpenIddictHostStartupGuard
{
    public const string CertificatePathKey = "OpenIddict:Certificate:Path";

    public static void EnsureProductionCertificateConfigured(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var certPath = configuration[CertificatePathKey];
        if (string.IsNullOrWhiteSpace(certPath))
        {
            throw new InvalidOperationException(
                "OpenIddict:Certificate:Path is required in non-Development environments so every instance " +
                "loads the same signing/encryption PFX behind a load balancer.");
        }
    }
}
