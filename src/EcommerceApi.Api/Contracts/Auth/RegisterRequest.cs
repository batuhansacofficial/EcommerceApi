using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Auth
{
    public sealed record RegisterRequest(
    [Required]
    [EmailAddress]
    [StringLength(320)]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 6)]
    string Password,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string FirstName,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string LastName);
}