using System;
using System.Collections.Generic;

namespace Zahy.Identity.OpenIddict;

public class PartnerM2MClientProvisionRequest
{
    public Guid PartnerId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; set; } = Array.Empty<string>();
}

public class PartnerM2MClientProvisionResult
{
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Plaintext secret returned once to the caller; never persisted by Zahy.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}

public class PartnerM2MClientRotateResult
{
    public string ClientId { get; set; } = string.Empty;

    /// <summary>New plaintext secret returned once; never persisted by Zahy.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}
