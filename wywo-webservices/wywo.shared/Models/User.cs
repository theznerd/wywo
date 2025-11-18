using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wywo.shared.Models
{
    public record User(
        string Id,
        string Email,
        string? DisplayName,
        string? AvatarUrl,
        List<UserLogin> Logins);

    public record UserLogin(
        string Provider,
        string ProviderUserId,
        string? Email, 
        string? DisplayName);
}
