using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;
using Zahy.Hosting;

namespace Zahy.HttpApi.Host.Tests.Hosting;

public class OpenIddictHostStartupGuardTests
{
    [Fact]
    public void Production_without_certificate_path_fails_fast()
    {
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var configuration = new ConfigurationBuilder().Build();

        var exception = Should.Throw<InvalidOperationException>(() =>
            OpenIddictHostStartupGuard.EnsureProductionCertificateConfigured(environment, configuration));

        exception.Message.ShouldContain("OpenIddict:Certificate:Path");
    }

    [Fact]
    public void Production_with_certificate_path_does_not_throw()
    {
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [OpenIddictHostStartupGuard.CertificatePathKey] = "/secrets/openiddict.pfx"
            })
            .Build();

        Should.NotThrow(() =>
            OpenIddictHostStartupGuard.EnsureProductionCertificateConfigured(environment, configuration));
    }

    [Fact]
    public void Development_without_certificate_path_is_allowed()
    {
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var configuration = new ConfigurationBuilder().Build();

        Should.NotThrow(() =>
            OpenIddictHostStartupGuard.EnsureProductionCertificateConfigured(environment, configuration));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Zahy.HttpApi.Host.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
