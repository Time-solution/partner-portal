using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.ServiceOrders;

/// <summary>
/// Gate 2b — service order lifecycle. COMPUTE-ONLY: no posting, no invoice, no payment, no escrow,
/// no settlement wiring. Merchant methods are tenant-scoped; partner methods partner-scoped —
/// foreign orders are structurally invisible. The admin sweep is the v1 auto-accept trigger
/// (no background-job infra in this gate).
/// </summary>
public interface IServiceOrderAppService : IApplicationService
{
    // Merchant
    Task<MerchantServiceOrderDto> CreateAsync(CreateServiceOrderInput input);

    Task<MerchantServiceOrderDto> SubmitRequirementsAsync(SubmitServiceOrderRequirementsInput input);

    Task<MerchantServiceOrderDto> AcceptDeliveryAsync(ServiceOrderActionInput input);

    Task<MerchantServiceOrderDto> RequestRevisionAsync(ServiceOrderActionInput input);

    Task<MerchantServiceOrderDto> CancelAsync(ServiceOrderActionInput input);

    Task<List<MerchantServiceOrderDto>> GetMyOrdersAsync();

    // Partner
    Task<PartnerServiceOrderDto> PartnerAcceptAsync(ServiceOrderActionInput input);

    Task<PartnerServiceOrderDto> PartnerDeclineAsync(ServiceOrderActionInput input);

    Task<PartnerServiceOrderDto> MarkDeliveredAsync(ServiceOrderActionInput input);

    Task<List<PartnerServiceOrderDto>> GetIncomingOrdersAsync();

    // Admin — v1 auto-accept trigger (mock/manual; the 7-day rule lives in the domain).
    Task<int> RunAutoAcceptSweepAsync();
}
