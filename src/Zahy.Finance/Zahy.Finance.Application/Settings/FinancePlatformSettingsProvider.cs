using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Zahy.Finance;

public class FinancePlatformSettingsProvider : ApplicationService
{
    private readonly IRepository<FinancePlatformSettings, Guid> _settingsRepository;

    public FinancePlatformSettingsProvider(IRepository<FinancePlatformSettings, Guid> settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<FinancePlatformSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.FindAsync(
            FinanceConsts.PlatformSettingsId,
            cancellationToken: cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        settings = FinancePlatformSettings.CreateDefault(FinanceConsts.PlatformSettingsId);
        await _settingsRepository.InsertAsync(settings, autoSave: true, cancellationToken: cancellationToken);
        return settings;
    }

    public async Task<InvoiceGenerationMode> GetDefaultInvoiceGenerationModeAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        return settings.DefaultInvoiceGenerationMode;
    }
}
