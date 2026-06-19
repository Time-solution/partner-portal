using System;
using System.Collections.Generic;

namespace Zahy.Webhooks;

public class WebhookFilterRules
{
    public List<Guid>? TenantIds { get; set; }

    public List<string>? Directions { get; set; }

    public decimal? MinAmountSar { get; set; }
}
