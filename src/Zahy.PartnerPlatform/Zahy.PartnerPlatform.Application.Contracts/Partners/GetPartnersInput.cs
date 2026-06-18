using System;
using Volo.Abp.Application.Dtos;

namespace Zahy.PartnerPlatform.Partners;

public class GetPartnersInput : PagedAndSortedResultRequestDto
{
    public PartnerStatus? Status { get; set; }

    public PartnerType? Type { get; set; }

    public string? Filter { get; set; }
}
