namespace Zahy.PartnerPlatform.Partners;

internal static class PartnerDtoMapper
{
    public static PartnerListItemDto ToListItem(Partner partner)
    {
        return new PartnerListItemDto
        {
            Id = partner.Id,
            Type = partner.Type,
            Status = partner.Status,
            LegalName = partner.LegalName,
            TradeName = partner.TradeName,
            PrimaryContactEmail = partner.PrimaryContactEmail,
            CreationTime = partner.CreationTime,
            MaskedIban = MaskIban(partner.BankInfo.Iban)
        };
    }

    public static PartnerDto ToDto(Partner partner)
    {
        return new PartnerDto
        {
            Id = partner.Id,
            Type = partner.Type,
            Status = partner.Status,
            LegalName = partner.LegalName,
            TradeName = partner.TradeName,
            ContactInfo = new ContactInfoDto
            {
                ContactName = partner.ContactInfo.ContactName,
                Email = partner.ContactInfo.Email,
                Phone = partner.ContactInfo.Phone,
                AddressLine1 = partner.ContactInfo.AddressLine1,
                AddressLine2 = partner.ContactInfo.AddressLine2,
                City = partner.ContactInfo.City,
                Region = partner.ContactInfo.Region,
                PostalCode = partner.ContactInfo.PostalCode,
                CountryCode = partner.ContactInfo.CountryCode
            },
            BankInfo = partner.BankInfo.IsEmpty
                ? null
                : new BankInfoDto
                {
                    BankName = partner.BankInfo.BankName,
                    AccountHolderName = partner.BankInfo.AccountHolderName,
                    Iban = partner.BankInfo.Iban,
                    SwiftCode = partner.BankInfo.SwiftCode
                },
            PrimaryContactEmail = partner.PrimaryContactEmail,
            RegistrantName = partner.RegistrantName,
            RegistrantPhone = partner.RegistrantPhone,
            CloseReason = partner.CloseReason,
            CloseNotes = partner.CloseNotes,
            OpenIddictClientId = partner.OpenIddictClientId,
            CreationTime = partner.CreationTime,
            HasCompleteBankInfo = partner.HasCompleteBankInfo()
        };
    }

    private static string? MaskIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban) || iban.Length < 4)
        {
            return null;
        }

        return $"****{iban[^4..]}";
    }
}
