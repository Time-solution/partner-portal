using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Settlement;

/// <summary>
/// A bank account in the accountant-managed registry (Track B). Master data the accountant adds via
/// settings; each bank is bound to a ledger sub-account <see cref="Code"/> (110x) beneath the 1100
/// parent. Payments may route to a bank → the receipt debits this bank's sub-account instead of the
/// generic 1100. The full <see cref="AccountNumber"/> is stored but only ever shown masked.
///
/// COMPUTE/REGISTRY ONLY — adding a bank does NOT mutate the production chart of accounts; the live
/// chart change stays gated (<c>SettlementEngineOptions.BankRegistryLiveChartEnabled</c>, default OFF).
/// </summary>
public class BankAccount : FullAuditedAggregateRoot<Guid>
{
    /// <summary>Ledger sub-account code (1101–1149) under the 1100 parent. Assigned once, never reused while active.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Full account number / IBAN — stored whole, displayed masked (last 4).</summary>
    public string AccountNumber { get; private set; } = string.Empty;

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public BankAccountStatus Status { get; private set; } = BankAccountStatus.Active;

    /// <summary>
    /// Optional gateway-route key for the FUTURE auto-route seam — a gateway payment WOULD locate its
    /// mapped bank via this value. SEAM ONLY: not consumed by any live gateway (gated phase).
    /// </summary>
    public string? GatewayMapping { get; private set; }

    protected BankAccount()
    {
    }

    public BankAccount(
        Guid id,
        string code,
        string name,
        string accountNumber,
        string currency,
        string? gatewayMapping = null)
        : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), SettlementLedgerAccountConsts.MaxCodeLength).Trim();
        Name = NormalizeName(name);
        AccountNumber = NormalizeAccountNumber(accountNumber);
        Currency = Check.NotNullOrWhiteSpace(currency, nameof(currency), SettlementBankAccountConsts.MaxCurrencyLength).Trim();
        GatewayMapping = NormalizeGateway(gatewayMapping);
        Status = BankAccountStatus.Active;
    }

    public BankAccount Update(string name, string accountNumber, string currency, string? gatewayMapping)
    {
        Name = NormalizeName(name);
        AccountNumber = NormalizeAccountNumber(accountNumber);
        Currency = Check.NotNullOrWhiteSpace(currency, nameof(currency), SettlementBankAccountConsts.MaxCurrencyLength).Trim();
        GatewayMapping = NormalizeGateway(gatewayMapping);
        return this;
    }

    public BankAccount Deactivate()
    {
        Status = BankAccountStatus.Inactive;
        return this;
    }

    public BankAccount Activate()
    {
        Status = BankAccountStatus.Active;
        return this;
    }

    /// <summary>Display value — full number masked to the last 4 characters.</summary>
    public string MaskedAccountNumber => BankLedgerCoding.Mask(AccountNumber);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(SettlementBankAccountErrorCodes.EmptyName);
        }

        return Check.NotNullOrWhiteSpace(name, nameof(name), SettlementBankAccountConsts.MaxNameLength).Trim();
    }

    private static string NormalizeAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new BusinessException(SettlementBankAccountErrorCodes.EmptyAccountNumber);
        }

        return Check.NotNullOrWhiteSpace(
            accountNumber, nameof(accountNumber), SettlementBankAccountConsts.MaxAccountNumberLength).Trim();
    }

    private static string? NormalizeGateway(string? gatewayMapping)
    {
        if (string.IsNullOrWhiteSpace(gatewayMapping))
        {
            return null;
        }

        return Check.Length(
            gatewayMapping.Trim(), nameof(gatewayMapping), SettlementBankAccountConsts.MaxGatewayMappingLength);
    }
}
