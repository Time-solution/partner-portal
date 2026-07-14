using System;
using System.Linq;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Cross-module PIN: the calendar-day proration rule lives in exactly ONE place
/// (<see cref="SettlementProration.ProrateByCalendarDays"/>). Both production callers —
/// <see cref="UsageBillingCalculator"/> (PartnerCatalog) and <see cref="InvoiceReader"/>'s subscription
/// line (Settlement) — must produce byte-identical output for the same amount/window/period, including
/// rounding-sensitive partial windows. If either caller re-implements the rule, this pins the drift.
/// </summary>
public class SharedProrationPinTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");

    [Fact]
    public void UsageCalculator_And_InvoiceReport_Prorate_Identically_Via_The_Shared_Rule()
    {
        var cases = new (decimal Amount, int ActiveDays, int Year, int Month)[]
        {
            (149m, 30, 2026, 6),      // full 30-day month → no proration
            (149m, 7, 2026, 6),       // 149×7/30 = 34.7666… → 34.77 (rounding-sensitive)
            (100.01m, 1, 2026, 2),    // 28-day Feb → 3.5717… → 3.57
            (999.99m, 17, 2026, 7),   // 31-day month partial
            (58.99m, 11, 2026, 6),    // 58.99×11/30 = 21.6296… → 21.63
            (1234.56m, 15, 2026, 2),  // midpoint-ish split
        };

        foreach (var (amount, activeDays, year, month) in cases)
        {
            var period = SettlementPeriod.Of(year, month);
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var expected = SettlementProration.ProrateByCalendarDays(amount, activeDays, daysInMonth);

            // Caller 1 — UsageBillingCalculator (subscription, zero usage → the line IS the prorated base).
            var pkg = new UsagePackage(Guid.NewGuid(), PartnerId, "Pin", "messages", UsagePackageMode.Subscription,
                "SAR", 1_000_000m, 0m, amount, 0m, 0m, ActivationFeePayer.Merchant);
            var usage = UsageBillingCalculator.Compute(pkg, usage: 0m, period, activeDays);
            usage.Fee!.BaseInclusive.ShouldBe(expected,
                $"UsageBillingCalculator diverged for {amount} × {activeDays}/{daysInMonth}");
            usage.Fee.TotalInclusive.ShouldBe(expected);

            // Caller 2 — InvoiceReader subscription line (Settlement).
            var invoice = InvoiceReader.Build(
                InvoiceEntityType.Merchant,
                Guid.NewGuid(),
                "PIN",
                period,
                new InvoiceLineInput[]
                {
                    new SubscriptionLineInput("pin-service", Money.Of(amount, "SAR", vatInclusive: true), activeDays),
                },
                Enumerable.Empty<Payment>());
            invoice.Lines.Count.ShouldBe(1);
            invoice.Lines[0].AmountInclusive.Amount.ShouldBe(expected,
                $"InvoiceReader diverged for {amount} × {activeDays}/{daysInMonth}");
        }
    }
}
