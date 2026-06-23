using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;
using Zahy.Hosting;

namespace Zahy.HttpApi.Host.Tests.Hosting;

public class ZahyDataProtectionConfiguratorTests
{
    [Fact]
    public void LocalFileSystem_provider_registers_data_protection()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "zahy-dp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        try
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:ApplicationName"] = "Zahy.PartnerPlatform.Test",
                    ["DataProtection:Provider"] = nameof(DataProtectionKeyRingProvider.LocalFileSystem),
                    ["DataProtection:Directory"] = Path.Combine(contentRoot, "keys")
                })
                .Build();

            var environment = new TestHostEnvironment { ContentRootPath = contentRoot };

            ZahyDataProtectionConfigurator.Configure(services, configuration, environment);

            var provider = services.BuildServiceProvider();
            var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
            var protector = dataProtection.CreateProtector("test");
            var protectedPayload = protector.Protect("payload");
            protector.Unprotect(protectedPayload).ShouldBe("payload");
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Redis_provider_without_connection_string_fails_fast()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Provider"] = nameof(DataProtectionKeyRingProvider.Redis)
            })
            .Build();

        var environment = new TestHostEnvironment();

        var exception = Should.Throw<InvalidOperationException>(() =>
            ZahyDataProtectionConfigurator.Configure(services, configuration, environment));

        exception.Message.ShouldContain("RedisConnectionString");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Zahy.HttpApi.Host.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(Path.GetTempPath());
    }
}
