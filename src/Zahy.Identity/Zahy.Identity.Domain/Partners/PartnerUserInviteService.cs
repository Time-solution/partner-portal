using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Zahy.Identity.Roles;

namespace Zahy.Identity.Partners;

public class PartnerUserInviteService : DomainService, IPartnerUserInviteService
{
    private readonly IdentityUserManager _userManager;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ZahyRoleAssignmentManager _roleAssignmentManager;

    public PartnerUserInviteService(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IGuidGenerator guidGenerator,
        ZahyRoleAssignmentManager roleAssignmentManager)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
        _roleAssignmentManager = roleAssignmentManager;
    }

    public virtual async Task<PartnerUserInviteResult> InviteAsync(PartnerUserInviteRequest request)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNullOrWhiteSpace(request.Email, nameof(request.Email));
        Check.NotNullOrWhiteSpace(request.Role, nameof(request.Role));

        if (request.PartnerId == Guid.Empty)
        {
            throw new ArgumentException("PartnerId is required.", nameof(request));
        }

        if (ZahyRoleRegistry.Find(request.Role) is not { Scope: ZahyRoleScope.Partner })
        {
            throw new BusinessException("Zahy:Identity:InvalidPartnerRole")
                .WithData("Role", request.Role);
        }

        var normalizedEmail = _userManager.NormalizeEmail(request.Email);
        var existing = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail, includeDetails: true);

        IdentityUser user;
        if (existing != null)
        {
            var existingPartnerId = GetPartnerId(existing);
            if (existingPartnerId.HasValue && existingPartnerId != request.PartnerId)
            {
                throw new BusinessException(ZahyIdentityErrorCodes.PartnerUserClaimConflict)
                    .WithData("Email", request.Email);
            }

            user = existing;
            await EnsurePartnerClaimAsync(user, request.PartnerId);
        }
        else
        {
            var userName = BuildUserName(request.Email);
            user = new IdentityUser(_guidGenerator.Create(), userName, request.Email, tenantId: null);
            (await _userManager.CreateAsync(user, GenerateTemporaryPassword())).CheckErrors();
            (await _userManager.AddClaimAsync(user, PartnerIdentityClaims.CreatePartnerIdClaim(request.PartnerId)))
                .CheckErrors();
        }

        if (request.BypassRoleAssignmentPolicy)
        {
            if (!await _userManager.IsInRoleAsync(user, request.Role))
            {
                (await _userManager.AddToRoleAsync(user, request.Role)).CheckErrors();
            }
        }
        else
        {
            await _roleAssignmentManager.GrantRoleAsync(user, request.Role);
        }

        var setPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        return new PartnerUserInviteResult
        {
            IdentityUserId = user.Id,
            Email = request.Email,
            SetPasswordToken = setPasswordToken
        };
    }

    private async Task EnsurePartnerClaimAsync(IdentityUser user, Guid partnerId)
    {
        var claim = user.Claims.FirstOrDefault(c => c.ClaimType == ZahyClaimTypes.PartnerId);
        if (claim == null)
        {
            (await _userManager.AddClaimAsync(user, PartnerIdentityClaims.CreatePartnerIdClaim(partnerId)))
                .CheckErrors();
            return;
        }

        if (claim.ClaimValue != partnerId.ToString("D"))
        {
            throw new BusinessException(ZahyIdentityErrorCodes.PartnerUserClaimConflict)
                .WithData("PartnerId", partnerId);
        }
    }

    private static Guid? GetPartnerId(IdentityUser user)
    {
        var claim = user.Claims.FirstOrDefault(c => c.ClaimType == ZahyClaimTypes.PartnerId);
        return claim != null && Guid.TryParse(claim.ClaimValue, out var id) ? id : null;
    }

    private static string BuildUserName(string email)
    {
        var localPart = email.Split('@')[0].Replace('.', '-');
        var userName = $"partner-{localPart}-{Guid.NewGuid():N}";
        return userName.Length > 256 ? userName[..256] : userName;
    }

    private static string GenerateTemporaryPassword()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes) + "Aa1!";
    }
}
