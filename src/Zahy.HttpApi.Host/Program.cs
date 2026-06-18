using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Autofac;
using Zahy;

var builder = WebApplication.CreateBuilder(args);

await builder.AddApplicationAsync<ZahyHostModule>(options =>
{
    options.UseAutofac();
});

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();