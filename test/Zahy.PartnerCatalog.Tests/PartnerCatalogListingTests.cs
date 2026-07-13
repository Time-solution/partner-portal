using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2a — structured listing domain: caps + invariants owned by the domain, app-managed
/// contiguous ordering (never DB IDENTITY), MultiChoice rules, optional-sections-clear, and the
/// anti-disintermediation policy on every merchant-facing field (including the pre-existing
/// brief / benefit hooks). Presentation-only — nothing here touches orders, money, or settlement.
/// </summary>
public class PartnerCatalogListingTests
{
    private static readonly DateTime At = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PartnerCatalogListing NewListing() =>
        PartnerCatalogListing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static ListingTextRowInput[] TextRows(int count, string prefix = "Step") =>
        Enumerable.Range(1, count).Select(i => new ListingTextRowInput($"{prefix} {i}")).ToArray();

    // ---- caps ------------------------------------------------------------------------------------

    [Fact]
    public void Row_Caps_Are_Enforced_Per_Section()
    {
        var listing = NewListing();

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                Enumerable.Range(1, 11).Select(i => new ListingRequirementInput($"Req {i}", ListingRequirementType.ShortText)).ToList(),
                null, null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingRowCapExceeded);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                null,
                Enumerable.Range(1, 6).Select(i => new ListingDeliverableInput($"Del {i}", 1)).ToList(),
                null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingRowCapExceeded);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(null, null, TextRows(11), null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingRowCapExceeded);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(null, null, null, TextRows(11, "Term"), null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingRowCapExceeded);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(null, null, null, null,
                Enumerable.Range(1, 11).Select(i => new ListingFaqInput($"Q{i}", $"A{i}")).ToList(), At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingRowCapExceeded);

        // At-cap passes (10/5/10/10/10).
        listing.ReplaceSections(
            Enumerable.Range(1, 10).Select(i => new ListingRequirementInput($"Req {i}", ListingRequirementType.ShortText)).ToList(),
            Enumerable.Range(1, 5).Select(i => new ListingDeliverableInput($"Del {i}", 1)).ToList(),
            TextRows(10), TextRows(10, "Term"),
            Enumerable.Range(1, 10).Select(i => new ListingFaqInput($"Q{i}", $"A{i}")).ToList(), At);
        listing.Requirements.Count.ShouldBe(10);
        listing.Deliverables.Count.ShouldBe(5);
    }

    [Fact]
    public void Field_Length_Caps_Are_Enforced_With_The_Field_Named()
    {
        var listing = NewListing();

        var tooLongTitle = new string('x', PartnerCatalogListingConsts.MaxRequirementTitleLength + 1);
        var ex = Should.Throw<BusinessException>(() => listing.ReplaceSections(
            new[] { new ListingRequirementInput(tooLongTitle, ListingRequirementType.ShortText) },
            null, null, null, null, At));
        ex.Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldTooLong);
        ex.Data["Field"].ShouldBe("RequirementTitle");

        Should.Throw<BusinessException>(() => listing.ReplaceSections(null, null,
                new[] { new ListingTextRowInput(new string('x', PartnerCatalogListingConsts.MaxExecutionStepLength + 1)) },
                null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldTooLong);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(null, null, null, null,
                new[] { new ListingFaqInput("Q", new string('x', PartnerCatalogListingConsts.MaxFaqAnswerLength + 1)) }, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldTooLong);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                new[] { new ListingRequirementInput("   ", ListingRequirementType.ShortText) },
                null, null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldRequired);
    }

    [Fact]
    public void Choice_Length_Over_80_Is_Rejected_As_FieldTooLong()
    {
        // Choice COUNT rules live under :054 (ListingChoicesInvalid); choice LENGTH is a field cap
        // and deliberately routes through :051 with the field named — domain owns the split.
        var listing = NewListing();
        var ex = Should.Throw<BusinessException>(() => listing.ReplaceSections(
            new[]
            {
                new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice,
                    new[] { new string('x', PartnerCatalogListingConsts.MaxChoiceLength + 1) })
            },
            null, null, null, null, At));
        ex.Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldTooLong);
        ex.Data["Field"].ShouldBe("RequirementChoice");

        // At-cap (80) passes.
        var ok = NewListing();
        ok.ReplaceSections(
            new[]
            {
                new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice,
                    new[] { new string('x', PartnerCatalogListingConsts.MaxChoiceLength) })
            },
            null, null, null, null, At);
        ok.Requirements.Single().Choices.Single().Length.ShouldBe(PartnerCatalogListingConsts.MaxChoiceLength);
    }

    [Fact]
    public void Blank_Required_Fields_Are_Rejected_Per_Section()
    {
        void ShouldRequire(Action action, string field)
        {
            var ex = Should.Throw<BusinessException>(action);
            ex.Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingFieldRequired);
            ex.Data["Field"].ShouldBe(field);
        }

        ShouldRequire(() => NewListing().ReplaceSections(
            new[] { new ListingRequirementInput("  ", ListingRequirementType.ShortText) }, null, null, null, null, At),
            "RequirementTitle");
        ShouldRequire(() => NewListing().ReplaceSections(null,
            new[] { new ListingDeliverableInput("", 1) }, null, null, null, At),
            "DeliverableTitle");
        ShouldRequire(() => NewListing().ReplaceSections(null, null,
            new[] { new ListingTextRowInput("  ") }, null, null, At),
            "ExecutionStep");
        ShouldRequire(() => NewListing().ReplaceSections(null, null, null,
            new[] { new ListingTextRowInput("") }, null, At),
            "Term");
        ShouldRequire(() => NewListing().ReplaceSections(null, null, null, null,
            new[] { new ListingFaqInput(" ", "A") }, At),
            "FaqQuestion");
        ShouldRequire(() => NewListing().ReplaceSections(null, null, null, null,
            new[] { new ListingFaqInput("Q", "") }, At),
            "FaqAnswer");
    }

    // ---- ordering (app-managed) -------------------------------------------------------------------

    [Fact]
    public void Ordering_Is_App_Managed_Contiguous_And_Reorder_Persists()
    {
        var listing = NewListing();

        listing.ReplaceSections(null, null, TextRows(3), null, null, At);
        listing.ExecutionSteps.Select(s => s.OrderIndex).ShouldBe(new[] { 0, 1, 2 });
        listing.ExecutionSteps.Select(s => s.Text).ShouldBe(new[] { "Step 1", "Step 2", "Step 3" });

        // Reorder = resubmit in the new order; indexes are rewritten contiguous from zero.
        listing.ReplaceSections(null, null,
            new[] { new ListingTextRowInput("Step 3"), new ListingTextRowInput("Step 1"), new ListingTextRowInput("Step 2") },
            null, null, At);
        listing.ExecutionSteps.Select(s => s.OrderIndex).ShouldBe(new[] { 0, 1, 2 });
        listing.ExecutionSteps.Select(s => s.Text).ShouldBe(new[] { "Step 3", "Step 1", "Step 2" });
    }

    // ---- MultiChoice -------------------------------------------------------------------------------

    [Fact]
    public void Choices_Are_Only_For_MultiChoice_And_Capped()
    {
        var listing = NewListing();

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                new[] { new ListingRequirementInput("Pick", ListingRequirementType.ShortText, new[] { "A" }) },
                null, null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingChoicesOnlyForMultiChoice);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                new[] { new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice) },
                null, null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingChoicesInvalid);

        Should.Throw<BusinessException>(() => listing.ReplaceSections(
                new[] { new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice,
                    Enumerable.Range(1, 9).Select(i => $"C{i}").ToArray()) },
                null, null, null, null, At))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingChoicesInvalid);

        listing.ReplaceSections(
            new[] { new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice, new[] { "Basic", "Pro" }) },
            null, null, null, null, At);
        listing.Requirements.Single().Choices.ShouldBe(new[] { "Basic", "Pro" });

        // FileUpload is only a named TYPE at authoring time — no upload machinery in this gate.
        listing.ReplaceSections(
            new[] { new ListingRequirementInput("Brand assets", ListingRequirementType.FileUpload) },
            null, null, null, null, At);
        listing.Requirements.Single().Type.ShouldBe(ListingRequirementType.FileUpload);
    }

    [Fact]
    public void Deliverable_Quantity_Must_Be_1_To_99()
    {
        var listing = NewListing();

        foreach (var quantity in new[] { 0, 100 })
        {
            Should.Throw<BusinessException>(() => listing.ReplaceSections(null,
                    new[] { new ListingDeliverableInput("Reports", quantity) }, null, null, null, At))
                .Code.ShouldBe(PartnerCatalogListingErrorCodes.ListingDeliverableQuantityOutOfRange);
        }

        listing.ReplaceSections(null, new[] { new ListingDeliverableInput("Reports", 99) }, null, null, null, At);
        listing.Deliverables.Single().Quantity.ShouldBe(99);
    }

    // ---- optionality --------------------------------------------------------------------------------

    [Fact]
    public void Empty_Sections_Clear_And_All_Sections_Are_Optional()
    {
        var listing = NewListing();
        listing.ReplaceSections(
            new[] { new ListingRequirementInput("Logo", ListingRequirementType.FileUpload) },
            new[] { new ListingDeliverableInput("Posts", 8) },
            TextRows(2), TextRows(2, "Term"),
            new[] { new ListingFaqInput("Q", "A") }, At);
        listing.IsEmpty.ShouldBeFalse();

        listing.ReplaceSections(null, null, null, null, null, At);
        listing.IsEmpty.ShouldBeTrue();
        listing.Requirements.ShouldBeEmpty();
        listing.Deliverables.ShouldBeEmpty();
        listing.ExecutionSteps.ShouldBeEmpty();
        listing.Terms.ShouldBeEmpty();
        listing.Faqs.ShouldBeEmpty();
    }

    // ---- anti-disintermediation ----------------------------------------------------------------------

    [Theory]
    [InlineData("Visit https://example.com for details")]
    [InlineData("see www.mysite.sa now")]
    [InlineData("email me at ops@vendor.com")]
    [InlineData("call +966 55 123 4567 anytime")]
    [InlineData("whatsapp 0551234567")]
    public void Contact_Channels_Are_Rejected_In_Every_Merchant_Facing_Listing_Field(string dirty)
    {
        var listing = NewListing();

        void ShouldReject(Action action) =>
            Should.Throw<BusinessException>(action)
                .Code.ShouldBe(PartnerCatalogListingErrorCodes.MerchantFacingContactInfoNotAllowed);

        ShouldReject(() => listing.ReplaceSections(
            new[] { new ListingRequirementInput(dirty, ListingRequirementType.ShortText) }, null, null, null, null, At));
        ShouldReject(() => listing.ReplaceSections(
            new[] { new ListingRequirementInput("Pick", ListingRequirementType.MultiChoice, new[] { dirty } ) }, null, null, null, null, At));
        ShouldReject(() => listing.ReplaceSections(null,
            new[] { new ListingDeliverableInput(dirty, 1) }, null, null, null, At));
        ShouldReject(() => listing.ReplaceSections(null, null,
            new[] { new ListingTextRowInput(dirty) }, null, null, At));
        ShouldReject(() => listing.ReplaceSections(null, null, null,
            new[] { new ListingTextRowInput(dirty) }, null, At));
        ShouldReject(() => listing.ReplaceSections(null, null, null, null,
            new[] { new ListingFaqInput("Q", dirty) }, At));
        ShouldReject(() => listing.ReplaceSections(null, null, null, null,
            new[] { new ListingFaqInput(dirty, "A") }, At));
    }

    [Fact]
    public void Clean_Text_Passes_The_Content_Policy()
    {
        PartnerCatalogContentPolicy.ContainsContactChannel(
            "نوفّر ٥ منشورات شهريًا مع تقرير أداء وتسليم خلال 3 أيام").ShouldBeFalse();
        PartnerCatalogContentPolicy.ContainsContactChannel(
            "Two revision rounds included. Delivery within 5 business days.").ShouldBeFalse();
        PartnerCatalogContentPolicy.ContainsContactChannel(null).ShouldBeFalse();

        var listing = NewListing();
        listing.ReplaceSections(null, null, new[] { new ListingTextRowInput("We deliver within 5 days") }, null, null, At);
        listing.ExecutionSteps.Single().Text.ShouldBe("We deliver within 5 days");
    }

    [Fact]
    public void Existing_Merchant_Facing_Fields_Are_Hooked_Into_The_Same_Policy()
    {
        // Partner brief (profile).
        Should.Throw<BusinessException>(() =>
                PartnerCatalogProfile.Create(Guid.NewGuid(), Guid.NewGuid(), "تواصل معنا على www.vendor.sa"))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.MerchantFacingContactInfoNotAllowed);

        // Merchant benefit (offering) — via the item factory.
        Should.Throw<BusinessException>(() => PartnerCatalogItem.Create(
                Guid.NewGuid(), Guid.NewGuid(), "SVC-X", "Service", null,
                PartnerCatalogOfferingKind.ServiceOneOff, Money.Of(70m, vatInclusive: true),
                merchantBenefit: "email sales@vendor.com"))
            .Code.ShouldBe(PartnerCatalogListingErrorCodes.MerchantFacingContactInfoNotAllowed);

        // Clean values still pass both.
        PartnerCatalogProfile.Create(Guid.NewGuid(), Guid.NewGuid(), "نبذة نظيفة عن الشركة").PartnerBrief
            .ShouldBe("نبذة نظيفة عن الشركة");
    }

    // ---- structural scope (6b standard) -----------------------------------------------------------------

    [Fact]
    public void Populated_Listing_Object_Graph_Carries_No_Buy_Margin_Or_BuyRate_Members()
    {
        var listing = NewListing();
        listing.ReplaceSections(
            new[] { new ListingRequirementInput("Logo", ListingRequirementType.FileUpload) },
            new[] { new ListingDeliverableInput("Posts", 8) },
            TextRows(2), TextRows(2, "Term"),
            new[] { new ListingFaqInput("How fast?", "Five days.") }, At);

        var forbidden = new[] { "buy", "buyrate", "margin", "partnercost", "basebuyamount", "overagebuyamount" };

        foreach (var type in new[]
                 {
                     typeof(PartnerCatalogListing), typeof(ListingRequirement), typeof(ListingDeliverable),
                     typeof(ListingExecutionStep), typeof(ListingTerm), typeof(ListingFaq),
                     typeof(Write.PartnerCatalogListingDto), typeof(Write.ListingRequirementDto),
                     typeof(Write.ListingDeliverableDto), typeof(Write.ListingTextRowDto), typeof(Write.ListingFaqDto),
                 })
        {
            foreach (var property in type.GetProperties())
            {
                forbidden.ShouldNotContain(property.Name.ToLowerInvariant(),
                    $"{type.Name}.{property.Name} leaks a cost/margin member into the listing graph");
            }
        }

        // And the populated instance serializes with zero cost/margin keys (deep, value-level).
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            listing.Requirements,
            listing.Deliverables,
            listing.ExecutionSteps,
            listing.Terms,
            listing.Faqs
        }).ToLowerInvariant();
        foreach (var key in forbidden)
        {
            json.ShouldNotContain($"\"{key}\"");
        }
    }
}
