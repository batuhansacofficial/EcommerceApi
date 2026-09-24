using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Auth
{
    public sealed record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(320)]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 6)]
    string Password);
}