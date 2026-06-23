using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Autofac;
using Zahy;
using Zahy.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutofac();

OpenIddictHostStartupGuard.EnsureProductionCertificateConfigured(builder.Environment, builder.Configuration);

await builder.AddApplicationAsync<ZahyHostModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();