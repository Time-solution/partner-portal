namespace Zahy.PartnerPlatform.Partners;

/// <summary>Roles that may be assigned through partner team management.</summary>
public static class PartnerTeamRoles
{
    public const string Manager = "Partner.Manager";
    public const string Staff = "Partner.Staff";

    public static readonly string[] InvitableRoles = [Manager, Staff];

    public static bool IsInvitable(string role) =>
        role == Manager || role == Staff;
}
