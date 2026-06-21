using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementPostingFeatureFlagTests
{
    [Fact]
    public void Posting_Is_Off_By_Default()
    {
        new SettlementEngineOptions().PostingEnabled.ShouldBeFalse();
    }
}
