using System;
using System.Collections.Generic;

namespace Zahy.PartnerCatalog.Write;

// NOTE (established rule): DTOs carry NO validation annotations tied to the real error codes —
// the DOMAIN (PartnerCatalogListing) owns every cap and invariant.

public class ListingRequirementDto
{
    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public ListingRequirementType Type { get; set; } = ListingRequirementType.ShortText;

    /// <summary>MultiChoice only (≤8, each ≤80). Must be empty for every other type.</summary>
    public List<string> Choices { get; set; } = new();
}

public class ListingDeliverableDto
{
    public int OrderIndex { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
}

public class ListingTextRowDto
{
    public int OrderIndex { get; set; }

    public string Text { get; set; } = string.Empty;
}

public class ListingFaqDto
{
    public int OrderIndex { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;
}

/// <summary>Whole-document replace of the structured listing; empty sections CLEAR.</summary>
public class UpdatePartnerCatalogListingInput
{
    public List<ListingRequirementDto> Requirements { get; set; } = new();

    public List<ListingDeliverableDto> Deliverables { get; set; } = new();

    public List<ListingTextRowDto> ExecutionSteps { get; set; } = new();

    public List<ListingTextRowDto> Terms { get; set; } = new();

    public List<ListingFaqDto> Faqs { get; set; } = new();
}

public class PartnerCatalogListingDto
{
    public Guid PartnerCatalogItemId { get; set; }

    public List<ListingRequirementDto> Requirements { get; set; } = new();

    public List<ListingDeliverableDto> Deliverables { get; set; } = new();

    public List<ListingTextRowDto> ExecutionSteps { get; set; } = new();

    public List<ListingTextRowDto> Terms { get; set; } = new();

    public List<ListingFaqDto> Faqs { get; set; } = new();
}
