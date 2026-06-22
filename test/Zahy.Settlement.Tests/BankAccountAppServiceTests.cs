using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Zahy.Settlement.BankRegistry;
using Zahy.Settlement.Read;

namespace Zahy.Settlement;

/// <summary>
/// Track B — integration over the bank registry write app service. The accountant adds banks; each gets
/// the next 110x sub-account under 1100, and the account number is returned MASKED.
/// </summary>
public class BankAccountAppServiceTests : ZahySettlementReadTestBase
{
    private readonly IBankAccountAppService _service;

    public BankAccountAppServiceTests()
    {
        _service = GetRequiredService<IBankAccountAppService>();
    }

    [Fact]
    public async Task Adding_Banks_Assigns_Sequential_SubAccounts_Under_1100()
    {
        BankAccountDto first = null!;
        BankAccountDto second = null!;
        List<BankAccountDto> list = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _service.CreateAsync(new CreateBankAccountInput
            {
                Name = "National Commercial Bank",
                AccountNumber = "SA0380000000608010167519",
                Currency = "SAR",
            });

            second = await _service.CreateAsync(new CreateBankAccountInput
            {
                Name = "Al Rajhi Bank",
                AccountNumber = "SA4420000001234567891234",
                Currency = "SAR",
                GatewayMapping = "gw-rajhi-002",
            });

            list = await _service.GetListAsync();
        });

        // Sequential sub-accounts beneath the 1100 parent.
        first.Code.ShouldBe("1101");
        first.ParentCode.ShouldBe("1100");
        second.Code.ShouldBe("1102");

        // The full account number is never returned — only the masked last 4.
        first.MaskedAccountNumber.ShouldEndWith("7519");
        first.MaskedAccountNumber.ShouldNotContain("SA03");
        first.Status.ShouldBe(BankAccountStatus.Active);
        second.GatewayMapping.ShouldBe("gw-rajhi-002");

        list.Count.ShouldBe(2);
        list.Select(b => b.Code).ShouldBe(new[] { "1101", "1102" });
    }

    [Fact]
    public async Task Deactivate_Then_Activate_Toggles_Status()
    {
        BankAccountDto bank = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            bank = await _service.CreateAsync(new CreateBankAccountInput
            {
                Name = "Test Bank",
                AccountNumber = "1234567890",
                Currency = "SAR",
            });

            bank = await _service.DeactivateAsync(bank.Id);
            bank.Status.ShouldBe(BankAccountStatus.Inactive);

            bank = await _service.ActivateAsync(bank.Id);
            bank.Status.ShouldBe(BankAccountStatus.Active);
        });
    }
}
