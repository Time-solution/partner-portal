using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogItemReflectionTests
{
    [Fact]
    public void IsVisibleAt_Respects_Publish_Window()
    {
        var from = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var reflection = PartnerCatalogItemReflection.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            from,
            visibleTo: null,
            isPublished: true);

        reflection.IsVisibleAt(from.AddMinutes(-1)).ShouldBeFalse();
        reflection.IsVisibleAt(from).ShouldBeTrue();
        reflection.IsVisibleAt(from.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void Invalid_Visibility_Window_Is_Rejected()
    {
        var from = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        Should.Throw<BusinessException>(() =>
                PartnerCatalogItemReflection.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    from,
                    visibleTo: from.AddDays(-1),
                    isPublished: true))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidReflectionWindow);
    }

    [Fact]
    public void Denormalizes_PartnerId_For_Filter()
    {
        var partnerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var reflection = PartnerCatalogItemReflection.Create(
            Guid.NewGuid(),
            itemId,
            partnerId,
            DateTime.UtcNow,
            null,
            true);

        reflection.PartnerCatalogItemId.ShouldBe(itemId);
        reflection.PartnerId.ShouldBe(partnerId);
    }
}
