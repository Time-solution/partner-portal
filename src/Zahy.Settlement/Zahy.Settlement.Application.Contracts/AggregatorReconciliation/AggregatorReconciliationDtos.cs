using System;
using System.Collections.Generic;

namespace Zahy.Settlement.AggregatorReconciliation;

// NOTE (established rule): DTOs carry NO validation annotations tied to the real error codes —
// the DOMAIN owns every invariant (mandatory notes, two-person, immutability, tolerances).

public class ImportAggregatorStatementRequest
{
    public Guid PartnerId { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }

    public decimal DeclaredGross { get; set; }

    public decimal DeclaredFees { get; set; }

    public decimal DeclaredNet { get; set; }

    public string Currency { get; set; } = "SAR";

    public List<ImportAggregatorStatementLine> Lines { get; set; } = new();
}

public class ImportAggregatorStatementLine
{
    public string ExternalOrderRef { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public decimal Gross { get; set; }

    public decimal AggregatorFee { get; set; }

    public decimal Net { get; set; }
}

public class ImportAggregatorStatementResult
{
    public Guid StatementId { get; set; }

    /// <summary>False when the identical content was already imported (no-op, zero new rows).</summary>
    public bool IsNew { get; set; }
}

public class AggregatorStatementDto
{
    public Guid Id { get; set; }

    public Guid PartnerId { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }

    public decimal DeclaredGross { get; set; }

    public decimal DeclaredFees { get; set; }

    public decimal DeclaredNet { get; set; }

    public string Currency { get; set; } = "SAR";

    public string Status { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; }

    public DateTime? MatchedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? ClosedBy { get; set; }

    public int LineCount { get; set; }

    public int OpenExceptionCount { get; set; }
}

public class AggregatorStatementLineDto
{
    public Guid Id { get; set; }

    public string ExternalOrderRef { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }

    public decimal Gross { get; set; }

    public decimal AggregatorFee { get; set; }

    public decimal Net { get; set; }

    public bool Matched { get; set; }
}

public class AggregatorStatementExceptionDto
{
    public Guid Id { get; set; }

    public Guid StatementId { get; set; }

    public Guid? StatementLineId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string ExternalOrderRef { get; set; } = string.Empty;

    public decimal? ExpectedAmount { get; set; }

    public decimal? ActualAmount { get; set; }

    public string Details { get; set; } = string.Empty;

    public bool Resolved { get; set; }

    public string? ResolutionNote { get; set; }

    public string? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }
}

public class AggregatorStatementDetailDto
{
    public AggregatorStatementDto Statement { get; set; } = new();

    public List<AggregatorStatementLineDto> Lines { get; set; } = new();

    public List<AggregatorStatementExceptionDto> Exceptions { get; set; } = new();
}

public class ResolveAggregatorExceptionRequest
{
    public Guid ExceptionId { get; set; }

    /// <summary>Mandatory — enforced by the domain, not by an annotation.</summary>
    public string Note { get; set; } = string.Empty;
}

public class CloseAggregatorStatementRequest
{
    public Guid StatementId { get; set; }
}
