using System.ComponentModel.DataAnnotations;

namespace Zahy.Identity.Account;

public class LoginInput
{
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
