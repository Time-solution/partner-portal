using System;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerRegistrationResultDto
{
    public Guid PartnerId { get; set; }

    public PartnerStatus Status { get; set; }

    public DateTime SubmittedAt { get; set; }
}
