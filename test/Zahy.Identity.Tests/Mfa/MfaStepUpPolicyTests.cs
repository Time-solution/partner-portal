using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Zahy.Identity.Mfa;

public class MfaStepUpPolicyTests : ZahyIdentityTestBase
{
    private readonly IMfaStepUpPolicy _policy;

    public MfaStepUpPolicyTests()
    {
        _policy = GetRequiredService<IMfaStepUpPolicy>();
    }

    [Fact]
    public async Task StepUp_Should_Be_Disabled_By_Default()
    {
        (await _policy.IsStepUpRequiredAsync("DeletePartner")).ShouldBeFalse();
    }
}
