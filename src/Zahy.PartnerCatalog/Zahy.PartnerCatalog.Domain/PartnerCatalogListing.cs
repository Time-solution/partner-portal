using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2a — the structured listing for one offering (1:1 with <see cref="PartnerCatalogItem"/>),
/// sitting beside the 6a presentation fields: requirements ("what you'll need to provide"),
/// deliverables ("what you get"), execution steps, terms, and FAQs. All sections are optional and
/// an empty section CLEARS. The DOMAIN owns every cap and invariant (row caps, field lengths,
/// MultiChoice rules, quantity range, anti-disintermediation). Ordering is APP-MANAGED: rows are
/// rewritten with contiguous 0-based OrderIndex on every write and carry explicit Guid keys —
/// never DB IDENTITY sequence (the LineNo lesson). Presentation/config only — no order lifecycle,
/// no money, no settlement input; not draft-gated (the ratified 6b presentation convention).
/// </summary>
public class PartnerCatalogListing : AggregateRoot<Guid>
{
    private readonly List<ListingRequirement> _requirements = new();
    private readonly List<ListingDeliverable> _deliverables = new();
    private readonly List<ListingExecutionStep> _executionSteps = new();
    private readonly List<ListingTerm> _terms = new();
    private readonly List<ListingFaq> _faqs = new();

    public Guid PartnerCatalogItemId { get; private set; }

    public Guid PartnerId { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<ListingRequirement> Requirements => _requirements;

    public IReadOnlyList<ListingDeliverable> Deliverables => _deliverables;

    public IReadOnlyList<ListingExecutionStep> ExecutionSteps => _executionSteps;

    public IReadOnlyList<ListingTerm> Terms => _terms;

    public IReadOnlyList<ListingFaq> Faqs => _faqs;

    protected PartnerCatalogListing()
    {
    }

    private PartnerCatalogListing(Guid id, Guid partnerCatalogItemId, Guid partnerId)
        : base(id)
    {
        PartnerCatalogItemId = partnerCatalogItemId;
        PartnerId = partnerId;
    }

    public static PartnerCatalogListing Create(Guid id, Guid partnerCatalogItemId, Guid partnerId) =>
        new(id, partnerCatalogItemId, partnerId);

    /// <summary>
    /// Whole-document replace: validates every section then rewrites the rows with fresh keys and
    /// contiguous OrderIndex in the order supplied (reorder = resubmit in the new order). Empty or
    /// null sections clear. Throws before any mutation — the listing is never half-updated.
    /// </summary>
    public void ReplaceSections(
        IReadOnlyList<ListingRequirementInput>? requirements,
        IReadOnlyList<ListingDeliverableInput>? deliverables,
        IReadOnlyList<ListingTextRowInput>? executionSteps,
        IReadOnlyList<ListingTextRowInput>? terms,
        IReadOnlyList<ListingFaqInput>? faqs,
        DateTime at)
    {
        var newRequirements = BuildRequirements(requirements ?? Array.Empty<ListingRequirementInput>());
        var newDeliverables = BuildDeliverables(deliverables ?? Array.Empty<ListingDeliverableInput>());
        var newSteps = BuildTextRows(executionSteps ?? Array.Empty<ListingTextRowInput>(),
            PartnerCatalogListingConsts.MaxExecutionSteps, PartnerCatalogListingConsts.MaxExecutionStepLength,
            "ExecutionStep", (id, index, text) => new ListingExecutionStep(id, index, text));
        var newTerms = BuildTextRows(terms ?? Array.Empty<ListingTextRowInput>(),
            PartnerCatalogListingConsts.MaxTerms, PartnerCatalogListingConsts.MaxTermLength,
            "Term", (id, index, text) => new ListingTerm(id, index, text));
        var newFaqs = BuildFaqs(faqs ?? Array.Empty<ListingFaqInput>());

        _requirements.Clear();
        _requirements.AddRange(newRequirements);
        _deliverables.Clear();
        _deliverables.AddRange(newDeliverables);
        _executionSteps.Clear();
        _executionSteps.AddRange(newSteps);
        _terms.Clear();
        _terms.AddRange(newTerms);
        _faqs.Clear();
        _faqs.AddRange(newFaqs);
        UpdatedAt = at;
    }

    public bool IsEmpty =>
        _requirements.Count == 0 && _deliverables.Count == 0 &&
        _executionSteps.Count == 0 && _terms.Count == 0 && _faqs.Count == 0;

    private static List<ListingRequirement> BuildRequirements(IReadOnlyList<ListingRequirementInput> inputs)
    {
        EnsureRowCap(inputs.Count, PartnerCatalogListingConsts.MaxRequirements, "Requirements");

        var rows = new List<ListingRequirement>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var title = RequireText(input.Title, PartnerCatalogListingConsts.MaxRequirementTitleLength, "RequirementTitle");
            PartnerCatalogContentPolicy.EnsureNoContactChannel(title, "RequirementTitle");

            var choices = (input.Choices ?? Array.Empty<string>())
                .Select(c => (c ?? string.Empty).Trim())
                .Where(c => c.Length > 0)
                .ToList();

            if (input.Type != ListingRequirementType.MultiChoice)
            {
                if (choices.Count > 0)
                {
                    throw new BusinessException(PartnerCatalogListingErrorCodes.ListingChoicesOnlyForMultiChoice)
                        .WithData("Type", input.Type.ToString());
                }
            }
            else if (choices.Count == 0 || choices.Count > PartnerCatalogListingConsts.MaxChoicesPerRequirement)
            {
                throw new BusinessException(PartnerCatalogListingErrorCodes.ListingChoicesInvalid)
                    .WithData("Count", choices.Count)
                    .WithData("Max", PartnerCatalogListingConsts.MaxChoicesPerRequirement);
            }

            foreach (var choice in choices)
            {
                EnsureLength(choice, PartnerCatalogListingConsts.MaxChoiceLength, "RequirementChoice");
                PartnerCatalogContentPolicy.EnsureNoContactChannel(choice, "RequirementChoice");
            }

            rows.Add(new ListingRequirement(Guid.NewGuid(), i, title, input.Type, choices));
        }

        return rows;
    }

    private static List<ListingDeliverable> BuildDeliverables(IReadOnlyList<ListingDeliverableInput> inputs)
    {
        EnsureRowCap(inputs.Count, PartnerCatalogListingConsts.MaxDeliverables, "Deliverables");

        var rows = new List<ListingDeliverable>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var title = RequireText(input.Title, PartnerCatalogListingConsts.MaxDeliverableTitleLength, "DeliverableTitle");
            PartnerCatalogContentPolicy.EnsureNoContactChannel(title, "DeliverableTitle");

            if (input.Quantity < PartnerCatalogListingConsts.MinDeliverableQuantity ||
                input.Quantity > PartnerCatalogListingConsts.MaxDeliverableQuantity)
            {
                throw new BusinessException(PartnerCatalogListingErrorCodes.ListingDeliverableQuantityOutOfRange)
                    .WithData("Quantity", input.Quantity);
            }

            rows.Add(new ListingDeliverable(Guid.NewGuid(), i, title, input.Quantity));
        }

        return rows;
    }

    private static List<TRow> BuildTextRows<TRow>(
        IReadOnlyList<ListingTextRowInput> inputs,
        int rowCap,
        int lengthCap,
        string fieldName,
        Func<Guid, int, string, TRow> factory)
    {
        EnsureRowCap(inputs.Count, rowCap, fieldName + "s");

        var rows = new List<TRow>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            var text = RequireText(inputs[i].Text, lengthCap, fieldName);
            PartnerCatalogContentPolicy.EnsureNoContactChannel(text, fieldName);
            rows.Add(factory(Guid.NewGuid(), i, text));
        }

        return rows;
    }

    private static List<ListingFaq> BuildFaqs(IReadOnlyList<ListingFaqInput> inputs)
    {
        EnsureRowCap(inputs.Count, PartnerCatalogListingConsts.MaxFaqs, "Faqs");

        var rows = new List<ListingFaq>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            var question = RequireText(inputs[i].Question, PartnerCatalogListingConsts.MaxFaqQuestionLength, "FaqQuestion");
            var answer = RequireText(inputs[i].Answer, PartnerCatalogListingConsts.MaxFaqAnswerLength, "FaqAnswer");
            PartnerCatalogContentPolicy.EnsureNoContactChannel(question, "FaqQuestion");
            PartnerCatalogContentPolicy.EnsureNoContactChannel(answer, "FaqAnswer");
            rows.Add(new ListingFaq(Guid.NewGuid(), i, question, answer));
        }

        return rows;
    }

    private static void EnsureRowCap(int count, int cap, string section)
    {
        if (count > cap)
        {
            throw new BusinessException(PartnerCatalogListingErrorCodes.ListingRowCapExceeded)
                .WithData("Section", section)
                .WithData("Count", count)
                .WithData("Cap", cap);
        }
    }

    private static string RequireText(string? value, int lengthCap, string fieldName)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new BusinessException(PartnerCatalogListingErrorCodes.ListingFieldRequired)
                .WithData("Field", fieldName);
        }

        EnsureLength(trimmed, lengthCap, fieldName);
        return trimmed;
    }

    private static void EnsureLength(string value, int cap, string fieldName)
    {
        if (value.Length > cap)
        {
            throw new BusinessException(PartnerCatalogListingErrorCodes.ListingFieldTooLong)
                .WithData("Field", fieldName)
                .WithData("Length", value.Length)
                .WithData("Cap", cap);
        }
    }
}

// ---- write inputs (domain-owned shapes; DTOs map onto these, carrying no codes) -----------------

public sealed record ListingRequirementInput(string Title, ListingRequirementType Type, IReadOnlyList<string>? Choices = null);

public sealed record ListingDeliverableInput(string Title, int Quantity);

public sealed record ListingTextRowInput(string Text);

public sealed record ListingFaqInput(string Question, string Answer);

// ---- owned rows (explicit Guid keys; OrderIndex app-managed — never DB IDENTITY) ----------------

public class ListingRequirement
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ListingRequirementType Type { get; private set; }
    public string ChoicesJson { get; private set; } = "[]";

    protected ListingRequirement()
    {
    }

    internal ListingRequirement(Guid id, int orderIndex, string title, ListingRequirementType type, IReadOnlyList<string> choices)
    {
        Id = id;
        OrderIndex = orderIndex;
        Title = title;
        Type = type;
        ChoicesJson = System.Text.Json.JsonSerializer.Serialize(choices);
    }

    public IReadOnlyList<string> Choices =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(ChoicesJson) ?? new List<string>();
}

public class ListingDeliverable
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Quantity { get; private set; }

    protected ListingDeliverable()
    {
    }

    internal ListingDeliverable(Guid id, int orderIndex, string title, int quantity)
    {
        Id = id;
        OrderIndex = orderIndex;
        Title = title;
        Quantity = quantity;
    }
}

public class ListingExecutionStep
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Text { get; private set; } = string.Empty;

    protected ListingExecutionStep()
    {
    }

    internal ListingExecutionStep(Guid id, int orderIndex, string text)
    {
        Id = id;
        OrderIndex = orderIndex;
        Text = text;
    }
}

public class ListingTerm
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Text { get; private set; } = string.Empty;

    protected ListingTerm()
    {
    }

    internal ListingTerm(Guid id, int orderIndex, string text)
    {
        Id = id;
        OrderIndex = orderIndex;
        Text = text;
    }
}

public class ListingFaq
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;

    protected ListingFaq()
    {
    }

    internal ListingFaq(Guid id, int orderIndex, string question, string answer)
    {
        Id = id;
        OrderIndex = orderIndex;
        Question = question;
        Answer = answer;
    }
}
