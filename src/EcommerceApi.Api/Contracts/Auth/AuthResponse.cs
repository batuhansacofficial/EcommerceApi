namespace EcommerceApi.Api.Contracts.Auth
{
    public sealed record AuthResponse(
    string AccessToken,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role);
}