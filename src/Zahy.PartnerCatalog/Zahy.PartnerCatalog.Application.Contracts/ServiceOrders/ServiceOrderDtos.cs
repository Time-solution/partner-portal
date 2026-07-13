using System;
using System.Collections.Generic;

namespace Zahy.PartnerCatalog.ServiceOrders;

// NOTE (established rules): DTO annotations carry NO real error codes — the ServiceOrder domain
// owns every invariant. Scoping is STRUCTURAL (the 6b standard): the merchant shape carries
// SELL/fee only; the partner shape carries BUY only; margin exists on neither.

public class CreateServiceOrderInput
{
    public Guid PartnerCatalogItemId { get; set; }

    /// <summary>Optional declared payment plan (data only; validated in the domain).</summary>
    public List<ServiceOrderMilestoneDto> Milestones { get; set; } = new();
}

public class ServiceOrderMilestoneDto
{
    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class SubmitServiceOrderRequirementsInput
{
    public Guid OrderId { get; set; }

    /// <summary>Answer per listing requirement, keyed by the requirement's orderIndex.</summary>
    public Dictionary<int, string> AnswersByRequirementOrderIndex { get; set; } = new();
}

public class ServiceOrderActionInput
{
    public Guid OrderId { get; set; }

    /// <summary>Mandatory for decline / revision-request (domain-enforced).</summary>
    public string? Note { get; set; }
}

public class ServiceOrderAnswerDto
{
    public int OrderIndex { get; set; }

    public string RequirementTitle { get; set; } = string.Empty;

    public string RequirementType { get; set; } = string.Empty;

    public string AnswerText { get; set; } = string.Empty;
}

public class ServiceOrderHistoryDto
{
    public int OrderIndex { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime At { get; set; }
}

/// <summary>MERCHANT view — SELL/fee side only. No buy, no margin (structural).</summary>
public class MerchantServiceOrderDto
{
    public Guid Id { get; set; }

    public Guid PartnerCatalogItemId { get; set; }

    public string OfferingName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ParticipationMode { get; set; } = string.Empty;

    /// <summary>The merchant-facing price (sell under Principal, fee under SubscriptionFee).</summary>
    public decimal PriceAmount { get; set; }

    public string Currency { get; set; } = "SAR";

    public int RevisionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public List<ServiceOrderAnswerDto> Answers { get; set; } = new();

    public List<ServiceOrderMilestoneDto> Milestones { get; set; } = new();

    public List<ServiceOrderHistoryDto> History { get; set; } = new();
}

/// <summary>PARTNER view — BUY side only (their receivable). No sell, no fee, no margin (structural).</summary>
public class PartnerServiceOrderDto
{
    public Guid Id { get; set; }

    public Guid PartnerCatalogItemId { get; set; }

    public string OfferingName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ParticipationMode { get; set; } = string.Empty;

    /// <summary>The partner buy leg (null under SubscriptionFee — the fee is merchant-facing).</summary>
    public decimal? BuyAmount { get; set; }

    public string Currency { get; set; } = "SAR";

    public int RevisionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public List<ServiceOrderAnswerDto> Answers { get; set; } = new();

    public List<ServiceOrderHistoryDto> History { get; set; } = new();
}
