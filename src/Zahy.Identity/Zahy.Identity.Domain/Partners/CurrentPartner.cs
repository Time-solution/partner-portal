using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace Zahy.Identity.Partners;

/// <summary>Resolves the partner aggregate of the current principal (host-level isolation).</summary>
public interface ICurrentPartner
{
    Guid? Id { get; }
}

public class CurrentPartner : ICurrentPartner, ITransientDependency
{
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public CurrentPartner(ICurrentPrincipalAccessor principalAccessor)
    {
        _principalAccessor = principalAccessor;
    }

    public Guid? Id
    {
        get
        {
            var value = _principalAccessor.Principal?.FindFirst(ZahyClaimTypes.PartnerId)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
