using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2b — ServiceOrder domain: the full state machine with append-only history, mandatory
/// notes, the 7-day auto-accept boundary, typed requirement answers with title snapshots,
/// milestone-plan validation (existing money rounding), and immutable price snapshots.
/// </summary>
public class ServiceOrderTests
{
    private static readonly DateTime T0 = new(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = Guid.NewGuid();

    private static PartnerCatalogItem ServiceItem(
        decimal cost = 70m,
        SettlementParticipationMode mode = SettlementParticipationMode.Principal) =>
        PartnerCatalogItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SVC-ORD", "Managed WhatsApp", null,
            PartnerCatalogOfferingKind.ServiceOneOff, Money.Of(cost, vatInclusive: true),
            settlementParticipationMode: mode);

    private static ServiceOrder NewOrder(
        decimal sell = 100m,
        SettlementParticipationMode mode = SettlementParticipationMode.Principal,
        IReadOnlyList<ServiceOrderMilestoneInput>? milestones = null,
        PartnerCatalogItem? item = null) =>
        ServiceOrder.Create(
            Guid.NewGuid(), Tenant, item ?? ServiceItem(mode: mode),
            Money.Of(sell, vatInclusive: true), milestones, "merchant-1", T0);

    private static IReadOnlyList<ListingRequirement> Requirements() =>
        BuildListing().Requirements;

    private static PartnerCatalogListing BuildListing(string multiChoiceTitle = "الفئة المستهدفة")
    {
        var listing = PartnerCatalogListing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        listing.ReplaceSections(
            new[]
            {
                new ListingRequirementInput("نبذة عن المتجر", ListingRequirementType.ShortText),
                new ListingRequirementInput("شعار المتجر", ListingRequirementType.FileUpload),
                new ListingRequirementInput(multiChoiceTitle, ListingRequirementType.MultiChoice, new[] { "أفراد", "شركات" }),
            },
            null, null, null, null, T0);
        return listing;
    }

    private static Dictionary<int, string> ValidAnswers() => new()
    {
        [0] = "متجر عطور في الرياض",
        [1] = "logo-final-v2.png", // FileUpload = declared file NAME only — no storage in this gate
        [2] = "شركات",
    };

    private static ServiceOrder SubmittedOrder()
    {
        var order = NewOrder();
        order.SubmitRequirements(Requirements(), ValidAnswers(), "merchant-1", T0.AddMinutes(5));
        return order;
    }

    // ---- lifecycle: legal path + append-only history -------------------------------------------------

    [Fact]
    public void Full_Legal_Lifecycle_Appends_Ordered_History_With_Actors_And_Notes()
    {
        var order = SubmittedOrder();
        order.PartnerAccept("partner-1", T0.AddHours(1));
        order.Deliver("partner-1", T0.AddDays(1));
        order.RequestRevision("merchant-1", "النص يحتاج تعديلًا", T0.AddDays(2));
        order.Deliver("partner-1", T0.AddDays(3));
        order.MerchantAccept("merchant-1", T0.AddDays(4));
        order.Close("merchant-1", T0.AddDays(4));

        order.Status.ShouldBe(ServiceOrderStatus.Closed);
        order.RevisionCount.ShouldBe(1);

        var history = order.History;
        history.Select(h => h.OrderIndex).ShouldBe(Enumerable.Range(0, history.Count)); // append-only, contiguous
        history.Select(h => h.Action).ShouldBe(new[]
        {
            ServiceOrderAction.Created,
            ServiceOrderAction.RequirementsSubmitted,
            ServiceOrderAction.PartnerAccepted,
            ServiceOrderAction.Delivered,
            ServiceOrderAction.RevisionRequested,
            ServiceOrderAction.Delivered,
            ServiceOrderAction.MerchantAccepted,
            ServiceOrderAction.Closed,
        });
        history.Single(h => h.Action == ServiceOrderAction.RevisionRequested).Note.ShouldBe("النص يحتاج تعديلًا");
        history.Single(h => h.Action == ServiceOrderAction.PartnerAccepted).Actor.ShouldBe("partner-1");
        // Nothing overwritten: the first Delivered row still carries its original transition.
        history.First(h => h.Action == ServiceOrderAction.Delivered).FromStatus.ShouldBe(ServiceOrderStatus.InProgress);
    }

    // ---- illegal transitions + terminal immutability ---------------------------------------------------

    [Fact]
    public void Illegal_Transitions_Are_Rejected_With_The_Transition_Code()
    {
        // Skipping ahead: deliver straight from Draft.
        Should.Throw<BusinessException>(() => NewOrder().Deliver("p", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition);

        // Accept before delivery.
        var inProgress = SubmittedOrder();
        inProgress.PartnerAccept("p", T0);
        Should.Throw<BusinessException>(() => inProgress.MerchantAccept("m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition);

        // Cancel/decline are only legal BEFORE InProgress.
        Should.Throw<BusinessException>(() => inProgress.MerchantCancel("m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition);
        Should.Throw<BusinessException>(() => inProgress.PartnerDecline("p", "busy", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition);
    }

    [Fact]
    public void Terminal_States_Are_Immutable()
    {
        var cancelled = NewOrder();
        cancelled.MerchantCancel("m", T0);
        Should.Throw<BusinessException>(() => cancelled.SubmitRequirements(Requirements(), ValidAnswers(), "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OrderTerminalImmutable);

        var declined = SubmittedOrder();
        declined.PartnerDecline("p", "خارج نطاق خدماتنا", T0);
        Should.Throw<BusinessException>(() => declined.PartnerAccept("p", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OrderTerminalImmutable);

        var closed = SubmittedOrder();
        closed.PartnerAccept("p", T0);
        closed.Deliver("p", T0);
        closed.MerchantAccept("m", T0);
        closed.Close("m", T0);
        Should.Throw<BusinessException>(() => closed.RequestRevision("m", "late", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OrderTerminalImmutable);
    }

    [Fact]
    public void Decline_And_Revision_Require_A_Note()
    {
        Should.Throw<BusinessException>(() => SubmittedOrder().PartnerDecline("p", "  ", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OrderNoteRequired);

        var delivered = SubmittedOrder();
        delivered.PartnerAccept("p", T0);
        delivered.Deliver("p", T0);
        Should.Throw<BusinessException>(() => delivered.RequestRevision("m", "", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OrderNoteRequired);
    }

    // ---- 7-day auto-accept (the Salla calibration) ------------------------------------------------------

    [Fact]
    public void AutoAccept_Boundary_Is_Exactly_Seven_Days_With_System_Actor_And_Idempotent_Resweep()
    {
        var order = SubmittedOrder();
        order.PartnerAccept("p", T0);
        var deliveredAt = T0.AddDays(1);
        order.Deliver("p", deliveredAt);

        // 6d 23h 59m 59s after delivery — NOT due.
        order.AutoAcceptIfDue(deliveredAt.AddDays(7).AddSeconds(-1)).ShouldBeFalse();
        order.Status.ShouldBe(ServiceOrderStatus.Delivered);

        // Exactly 7 days — due; System actor recorded; closes immediately.
        order.AutoAcceptIfDue(deliveredAt.AddDays(7)).ShouldBeTrue();
        order.Status.ShouldBe(ServiceOrderStatus.Closed);
        order.History.Single(h => h.Action == ServiceOrderAction.AutoAccepted)
            .Actor.ShouldBe(PartnerCatalogServiceOrderConsts.SystemActor);
        order.History.Last().Action.ShouldBe(ServiceOrderAction.Closed);

        // Idempotent re-sweep: no double transition, no extra history rows.
        var historyCount = order.History.Count;
        order.AutoAcceptIfDue(deliveredAt.AddDays(8)).ShouldBeFalse();
        order.History.Count.ShouldBe(historyCount);
    }

    // ---- requirements intake ------------------------------------------------------------------------------

    [Fact]
    public void Submission_Requires_A_Non_Blank_Answer_Per_Requirement()
    {
        var missing = ValidAnswers();
        missing.Remove(1);
        Should.Throw<BusinessException>(() => NewOrder().SubmitRequirements(Requirements(), missing, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.AnswerMissingForRequirement);

        var blank = ValidAnswers();
        blank[0] = "   ";
        Should.Throw<BusinessException>(() => NewOrder().SubmitRequirements(Requirements(), blank, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.AnswerMissingForRequirement);
    }

    [Theory]
    [InlineData(ListingRequirementType.ShortText, PartnerCatalogServiceOrderConsts.MaxShortTextAnswerLength)]
    [InlineData(ListingRequirementType.LongText, PartnerCatalogServiceOrderConsts.MaxLongTextAnswerLength)]
    [InlineData(ListingRequirementType.Link, PartnerCatalogServiceOrderConsts.MaxLinkAnswerLength)]
    [InlineData(ListingRequirementType.FileUpload, PartnerCatalogServiceOrderConsts.MaxFileNameReferenceLength)]
    public void Typed_Answer_Caps_Are_Enforced(ListingRequirementType type, int cap)
    {
        var listing = PartnerCatalogListing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        listing.ReplaceSections(new[] { new ListingRequirementInput("حقل", type) }, null, null, null, null, T0);

        var over = new Dictionary<int, string> { [0] = new string('x', cap + 1) };
        Should.Throw<BusinessException>(() => NewOrder().SubmitRequirements(listing.Requirements, over, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.AnswerTooLong);

        var atCap = new Dictionary<int, string> { [0] = new string('x', cap) };
        var order = NewOrder();
        order.SubmitRequirements(listing.Requirements, atCap, "m", T0);
        order.Answers.Single().AnswerText.Length.ShouldBe(cap);
    }

    [Fact]
    public void Invalid_MultiChoice_Answer_Is_Rejected()
    {
        var wrong = ValidAnswers();
        wrong[2] = "حكومة"; // not among the authored choices
        Should.Throw<BusinessException>(() => NewOrder().SubmitRequirements(Requirements(), wrong, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.InvalidMultiChoiceAnswer);
    }

    [Fact]
    public void Merchant_Answers_May_Carry_Contact_Details_The_056_Policy_Does_Not_Apply()
    {
        // The buyer's own info is theirs to share — anti-disintermediation (:056) is for
        // PARTNER-authored content only. This answer would be REJECTED as partner content…
        const string contactAnswer = "تواصلوا معي على 0551234567 أو owner@mystore.sa";
        PartnerCatalogContentPolicy.ContainsContactChannel(contactAnswer).ShouldBeTrue();

        // …but SUBMITS SUCCESSFULLY as a merchant answer, stored verbatim.
        var listing = PartnerCatalogListing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        listing.ReplaceSections(
            new[] { new ListingRequirementInput("بيانات التواصل لتفعيل القناة", ListingRequirementType.ShortText) },
            null, null, null, null, T0);

        var order = NewOrder();
        order.SubmitRequirements(listing.Requirements, new Dictionary<int, string> { [0] = contactAnswer }, "m", T0);

        order.Status.ShouldBe(ServiceOrderStatus.RequirementsSubmitted);
        order.Answers.Single().AnswerText.ShouldBe(contactAnswer);
    }

    [Fact]
    public void Answers_Snapshot_The_Requirement_Title_At_Answer_Time()
    {
        var order = NewOrder();
        order.SubmitRequirements(BuildListing("الفئة المستهدفة").Requirements, ValidAnswers(), "m", T0);

        // The listing is edited later — the order keeps what the merchant saw.
        var editedListing = BuildListing("الفئة المستهدفة الجديدة");
        editedListing.Requirements.Any(r => r.Title == "الفئة المستهدفة الجديدة").ShouldBeTrue();

        order.Answers.Select(a => a.RequirementTitleSnapshot)
            .ShouldBe(new[] { "نبذة عن المتجر", "شعار المتجر", "الفئة المستهدفة" });
        order.Answers.Single(a => a.RequirementType == ListingRequirementType.FileUpload)
            .AnswerText.ShouldBe("logo-final-v2.png");
    }

    // ---- milestones (declared plan; existing money rounding) -----------------------------------------------

    private static ServiceOrderMilestoneInput[] Plan(params decimal[] amounts) =>
        amounts.Select((a, i) => new ServiceOrderMilestoneInput($"دفعة {i + 1}", a)).ToArray();

    [Fact]
    public void Milestones_Require_Price_At_Least_1000()
    {
        Should.Throw<BusinessException>(() => NewOrder(sell: 999.99m, milestones: Plan(500m, 499.99m)))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestonesNotAllowedBelowMinimum);
    }

    [Fact]
    public void Milestone_Row_Count_Must_Be_2_To_5()
    {
        Should.Throw<BusinessException>(() => NewOrder(sell: 1000m, milestones: Plan(1000m)))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneRowCountInvalid);

        Should.Throw<BusinessException>(() => NewOrder(sell: 1200m, milestones: Plan(480m, 144m, 144m, 144m, 144m, 144m)))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneRowCountInvalid);
    }

    [Fact]
    public void First_Milestone_Must_Be_At_Least_40_Percent()
    {
        // 39% rejected…
        Should.Throw<BusinessException>(() => NewOrder(sell: 1000m, milestones: Plan(390m, 610m)))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneFirstShareTooSmall);

        // …40% exactly passes.
        var order = NewOrder(sell: 1000m, milestones: Plan(400m, 600m));
        order.Milestones.First().Amount.ShouldBe(400m);
    }

    [Fact]
    public void Milestone_Sum_Must_Equal_The_Price_Exactly_Via_Existing_Rounding()
    {
        Should.Throw<BusinessException>(() => NewOrder(sell: 1000m, milestones: Plan(400m, 599.99m)))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneSumMismatch);

        // Σ == price through SettlementMoney rounding (40/30/30 on 1500).
        var order = NewOrder(sell: 1500m, milestones: Plan(600m, 450m, 450m));
        SettlementMoney.Round(order.Milestones.Sum(m => m.Amount)).ShouldBe(1500m);
        order.Milestones.Select(m => m.OrderIndex).ShouldBe(new[] { 0, 1, 2 }); // app-managed, contiguous
    }

    [Fact]
    public void Invalid_Milestone_Rows_Are_Rejected()
    {
        Should.Throw<BusinessException>(() => NewOrder(sell: 1000m,
                milestones: new[] { new ServiceOrderMilestoneInput("  ", 400m), new ServiceOrderMilestoneInput("دفعة", 600m) }))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneRowInvalid);

        Should.Throw<BusinessException>(() => NewOrder(sell: 1000m,
                milestones: new[] { new ServiceOrderMilestoneInput("دفعة", 0m), new ServiceOrderMilestoneInput("دفعة ٢", 1000m) }))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.MilestoneRowInvalid);
    }

    // ---- snapshots + orderability --------------------------------------------------------------------------

    [Fact]
    public void Principal_Snapshots_Buy_And_Sell_And_SubscriptionFee_Snapshots_Fee_Only()
    {
        var principal = NewOrder(sell: 100m, mode: SettlementParticipationMode.Principal);
        principal.BuySnapshotAmount.ShouldBe(70m);
        principal.SellSnapshotAmount.ShouldBe(100m);
        principal.FeeSnapshotAmount.ShouldBeNull();
        principal.MerchantPriceAmount.ShouldBe(100m);

        var fee = NewOrder(sell: 49m, mode: SettlementParticipationMode.SubscriptionFee);
        fee.FeeSnapshotAmount.ShouldBe(49m);
        fee.BuySnapshotAmount.ShouldBeNull();
        fee.SellSnapshotAmount.ShouldBeNull();
    }

    [Fact]
    public void Repricing_The_Offering_Never_Touches_An_Existing_Order()
    {
        var item = ServiceItem(cost: 70m);
        var order = ServiceOrder.Create(Guid.NewGuid(), Tenant, item,
            Money.Of(100m, vatInclusive: true), null, "m", T0);

        // Offering repriced AFTER order creation (draft-time reprice — the only mutable price path).
        item.UpdateDraft(item.Name, null, Money.Of(90m, vatInclusive: true));
        item.PartnerCost.Amount.ShouldBe(90m);

        // The order's immutable snapshot pair is unchanged.
        order.BuySnapshotAmount.ShouldBe(70m);
        order.SellSnapshotAmount.ShouldBe(100m);

        // A NEW order snapshots the new price — the pair is per-order, at order time.
        var later = ServiceOrder.Create(Guid.NewGuid(), Tenant, item,
            Money.Of(120m, vatInclusive: true), null, "m", T0.AddDays(1));
        later.BuySnapshotAmount.ShouldBe(90m);
    }

    [Fact]
    public void Non_Service_Kinds_And_ReflectionOnly_Are_Not_Orderable()
    {
        var delivery = PartnerCatalogItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), "DLV-1", "Delivery", null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder, Money.Of(10m, vatInclusive: true));
        Should.Throw<BusinessException>(() =>
                ServiceOrder.Create(Guid.NewGuid(), Tenant, delivery, Money.Of(15m, vatInclusive: true), null, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OfferingNotOrderable);

        var reflection = ServiceItem(mode: SettlementParticipationMode.ReflectionOnly);
        Should.Throw<BusinessException>(() =>
                ServiceOrder.Create(Guid.NewGuid(), Tenant, reflection, Money.Of(15m, vatInclusive: true), null, "m", T0))
            .Code.ShouldBe(PartnerCatalogServiceOrderErrorCodes.OfferingNotOrderable);
    }
}
