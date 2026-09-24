using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Api.Contracts.Orders;

public sealed record CheckoutRequest([Required, Range(0.01, double.MaxValue)] decimal? ExpectedTotal);
