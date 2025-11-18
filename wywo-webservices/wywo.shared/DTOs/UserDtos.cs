using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wywo.shared.DTOs
{
    public sealed record CreateUserDto(string? Id, string Email, string? DisplayName, string? AvatarUrl);
}
