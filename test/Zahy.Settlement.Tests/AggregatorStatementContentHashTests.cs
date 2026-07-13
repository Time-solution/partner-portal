using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>Import idempotency key: identical content ⇒ identical hash (line order ignored);
/// any content change ⇒ different hash.</summary>
public class AggregatorStatementContentHashTests
{
    private static readonly Guid Partner = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTime From = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

    private static List<AggregatorStatementContentHash.LineContent> Lines() => new()
    {
        new("ORD-1001", Day, 113.00m, 10.00m, 103.00m),
        new("ORD-1002", Day, 226.00m, 20.00m, 206.00m),
    };

    private static string Hash(IEnumerable<AggregatorStatementContentHash.LineContent> lines, decimal net = 309.00m) =>
        AggregatorStatementContentHash.Compute(Partner, "Jahez", From, To, 339.00m, 30.00m, net, "SAR", lines);

    [Fact]
    public void Identical_Content_Produces_The_Same_Hash()
    {
        Hash(Lines()).ShouldBe(Hash(Lines()));
    }

    [Fact]
    public void Line_Order_Does_Not_Change_The_Hash()
    {
        Hash(Lines()).ShouldBe(Hash(Lines().AsEnumerable().Reverse()));
    }

    [Fact]
    public void Any_Content_Change_Changes_The_Hash()
    {
        var changedAmount = Lines();
        changedAmount[0] = changedAmount[0] with { Gross = 113.01m };
        Hash(changedAmount).ShouldNotBe(Hash(Lines()));

        Hash(Lines(), net: 310.00m).ShouldNotBe(Hash(Lines()));
    }
}
