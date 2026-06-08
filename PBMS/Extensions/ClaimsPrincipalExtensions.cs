using System.Security.Claims;

namespace PBMS.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var claimValue = user.FindFirst("UserId")?.Value;
            return Guid.TryParse(claimValue, out var userId) ? userId : Guid.Empty;
        }
    }
}
