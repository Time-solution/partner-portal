using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// SEAM ONLY — documented placeholder for the GATED gateway phase. NOT WIRED.
///
/// In the future gated gateway phase, a payment arriving from a payment gateway WOULD:
///   1. carry a gateway identifier (e.g. provider merchant id / settlement account ref),
///   2. auto-locate its mapped bank via <see cref="BankAccount.GatewayMapping"/> (<see cref="ResolveBankCode"/>),
///   3. route the receipt to that bank's 110x sub-account, and
///   4. auto-reconcile the matched deposit against the expected receivable.
///
/// Today there is NO real gateway: <see cref="ResolveBankCode"/> is a pure lookup used only by tests
/// to prove the mapping is reachable, and <see cref="AutoReconcile"/> intentionally throws to prove the
/// auto-route/auto-reconcile path is NOT implemented. Manual routing (accountant picks the bank) is the
/// only active path. Do NOT implement a real gateway here — that is the separately-gated phase.
/// </summary>
public static class GatewayAutoRouteSeam
{
    /// <summary>Whether the auto-route seam is wired to a live gateway. Always false until the gated phase.</summary>
    public const bool IsWired = false;

    /// <summary>
    /// Pure lookup: which registered bank a gateway payment WOULD map to, by matching its gateway key
    /// against each bank's <see cref="BankAccount.GatewayMapping"/>. Returns the bank's 110x code, or
    /// null when nothing maps. This computes nothing financial and posts no journal.
    /// </summary>
    public static string? ResolveBankCode(
        string? gatewayKey,
        IEnumerable<(string Code, string? GatewayMapping)> banks)
    {
        if (string.IsNullOrWhiteSpace(gatewayKey))
        {
            return null;
        }

        var key = gatewayKey.Trim();
        foreach (var bank in banks)
        {
            if (!string.IsNullOrWhiteSpace(bank.GatewayMapping)
                && string.Equals(bank.GatewayMapping.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return bank.Code;
            }
        }

        return null;
    }

    /// <summary>
    /// NOT IMPLEMENTED — the gated gateway phase wires gateway → bank auto-route + auto-reconcile here.
    /// Throwing keeps the seam honest: no code path silently performs a real gateway reconciliation.
    /// </summary>
    public static void AutoReconcile(string gatewayKey, IEnumerable<(string Code, string? GatewayMapping)> banks)
    {
        _ = banks?.ToList();
        throw new NotSupportedException(
            "Gateway auto-route / auto-reconcile is a SEAM ONLY and is not wired. " +
            "It is enabled in the separately-gated gateway phase. Use manual bank routing.");
    }
}
