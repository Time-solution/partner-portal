using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Zahy.Identity.Mfa;

/// <summary>
/// Default step-up policy. While MFA is disabled (the default), no action
/// requires step-up. When enabled, every guarded action requires step-up until
/// a richer, action-aware policy replaces this.
/// </summary>
public class DefaultMfaStepUpPolicy : IMfaStepUpPolicy, ITransientDependency
{
    private readonly ZahyMfaOptions _options;

    public DefaultMfaStepUpPolicy(IOptions<ZahyMfaOptions> options)
    {
        _options = options.Value;
    }

    public Task<bool> IsStepUpRequiredAsync(string action)
    {
        return Task.FromResult(_options.Enabled);
    }
}
