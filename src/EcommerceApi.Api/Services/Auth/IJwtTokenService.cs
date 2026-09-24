using EcommerceApi.Api.Entities;

namespace EcommerceApi.Api.Services.Auth
{
    public interface IJwtTokenService
    {
        string CreateAccessToken(User user);
    }
}