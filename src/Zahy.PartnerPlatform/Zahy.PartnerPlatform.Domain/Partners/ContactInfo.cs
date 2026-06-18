namespace Zahy.PartnerPlatform.Partners;

/// <summary>Business contact details for a partner aggregate.</summary>
public class ContactInfo
{
    public string ContactName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string? AddressLine2 { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string Region { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = PartnerPlatformConsts.DefaultCountryCode;

    protected ContactInfo()
    {
    }

    public ContactInfo(
        string contactName,
        string email,
        string phone,
        string addressLine1,
        string? addressLine2,
        string city,
        string region,
        string postalCode,
        string countryCode)
    {
        ContactName = contactName;
        Email = email;
        Phone = phone;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        Region = region;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }
}
