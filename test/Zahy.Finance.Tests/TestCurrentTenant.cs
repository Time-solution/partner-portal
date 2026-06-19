using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Zahy.Finance;

public class TestCurrentTenant : ICurrentTenant, ISingletonDependency
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }

    public bool IsAvailable => Id.HasValue;

    public IDisposable Change(Guid? id, string? name = null)
    {
        var previousId = Id;
        var previousName = Name;
        Id = id;
        Name = name;
        return new RestoreScope(() =>
        {
            Id = previousId;
            Name = previousName;
        });
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly Action _restore;

        public RestoreScope(Action restore) => _restore = restore;

        public void Dispose() => _restore();
    }
}

public class TestCurrentTenantAccessor : ICurrentTenant
{
    private readonly TestCurrentTenant _currentTenant;

    public TestCurrentTenantAccessor(TestCurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public Guid? Id => _currentTenant.Id;

    public string? Name => _currentTenant.Name;

    public bool IsAvailable => _currentTenant.IsAvailable;

    public IDisposable Change(Guid? id, string? name = null) =>
        _currentTenant.Change(id, name);
}
