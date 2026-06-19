using System;
using Volo.Abp.DependencyInjection;
using Zahy.Identity.Partners;

namespace Zahy.Finance;

public class TestCurrentPartner : ICurrentPartner, ISingletonDependency
{
    public Guid? Id { get; set; }
}

public class TestCurrentPartnerAccessor : ICurrentPartner
{
    private readonly TestCurrentPartner _currentPartner;

    public TestCurrentPartnerAccessor(TestCurrentPartner currentPartner)
    {
        _currentPartner = currentPartner;
    }

    public Guid? Id => _currentPartner.Id;
}
