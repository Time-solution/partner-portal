using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// One order row on an aggregator statement (Pattern A — delivery/fulfilment per order).
/// Gross is the ReflectionOnly merchant sale (never Zahy revenue); the aggregator fee is OUR BUY
/// side (Zahy is principal). All amounts are VAT-inclusive, matching the statement as issued.
/// Lines are import-immutable — variances become exception rows, never line edits.
/// </summary>
public class AggregatorStatementLine : Entity<Guid>
{
    public Guid StatementId { get; private set; }

    public string ExternalOrderRef { get; private set; } = string.Empty;

    public DateTime OrderDate { get; private set; }

    public decimal Gross { get; private set; }

    public decimal AggregatorFee { get; private set; }

    public decimal Net { get; private set; }

    protected AggregatorStatementLine()
    {
    }

    public static AggregatorStatementLine Create(
        Guid id,
        Guid statementId,
        string externalOrderRef,
        DateTime orderDate,
        decimal gross,
        decimal aggregatorFee,
        decimal net)
    {
        if (string.IsNullOrWhiteSpace(externalOrderRef))
        {
            throw new BusinessException(SettlementAggregatorStatementErrorCodes.StatementImportInvalid)
                .WithData("Reason", "EmptyExternalOrderRef");
        }

        return new AggregatorStatementLine
        {
            Id = id,
            StatementId = statementId,
            ExternalOrderRef = externalOrderRef.Trim(),
            OrderDate = orderDate,
            Gross = SettlementMoney.Round(gross),
            AggregatorFee = SettlementMoney.Round(aggregatorFee),
            Net = SettlementMoney.Round(net)
        };
    }
}
