using System.Security.Claims;
using EcommerceApi.Api.Contracts.Orders;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/checkout")]
    public sealed class CheckoutController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CheckoutController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        public async Task<ActionResult<OrderResponse>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var key = Request.Headers["Idempotency-Key"].ToString();
            if (!Guid.TryParse(key, out var parsedKey))
                return BadRequest(new { message = "Idempotency-Key must be a UUID. Reuse it when retrying the same checkout." });
            key = parsedKey.ToString();
            await using var transaction = await CartTransaction.BeginAsync(_dbContext, userId.Value, cancellationToken);
            var previousOrder = await _dbContext.Orders.Include(order => order.Items)
                .SingleOrDefaultAsync(order => order.UserId == userId.Value && order.CheckoutKey == key, cancellationToken);
            if (previousOrder is not null)
            {
                if (previousOrder.TotalAmount != request.ExpectedTotal)
                    return Conflict(new { message = "This checkout key was already used with another total." });
                return Ok(ToOrderResponse(previousOrder));
            }

            var cartItems = await _dbContext.CartItems
                .Include(cartItem => cartItem.Product)
                .Where(cartItem => cartItem.UserId == userId.Value)
                .OrderBy(cartItem => cartItem.ProductId)
                .ToListAsync(cancellationToken);

            if (cartItems.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Cart is empty."
                });
            }

            if (cartItems.Sum(item => item.Quantity * item.Product.Price) != request.ExpectedTotal)
                return Conflict(new { message = "Cart prices changed. Refresh the bag and confirm the new total." });

            foreach (var cartItem in cartItems)
            {
                if (!cartItem.Product.IsActive)
                {
                    return Conflict(new
                    {
                        message = $"Product '{cartItem.Product.Name}' is no longer available."
                    });
                }

                // The conditional UPDATE serializes stock decrements at the database row.
                // A competing checkout cannot spend the same units.
                var updated = await _dbContext.Products
                    .Where(product =>
                        product.Id == cartItem.ProductId &&
                        product.IsActive &&
                        product.Price == cartItem.Product.Price &&
                        product.StockQuantity >= cartItem.Quantity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        product => product.StockQuantity,
                        product => product.StockQuantity - cartItem.Quantity)
                        .SetProperty(product => product.Version, product => product.Version + 1)
                        .SetProperty(product => product.UpdatedAtUtc, DateTime.UtcNow),
                        cancellationToken);

                if (updated != 1)
                {
                    return Conflict(new
                    {
                        message = $"Price, availability or stock changed for '{cartItem.Product.Name}'. Refresh the cart and try again."
                    });
                }
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                CheckoutKey = key,
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            };

            foreach (var cartItem in cartItems)
            {
                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.Product.Name,
                    ProductSku = cartItem.Product.Sku,
                    UnitPrice = cartItem.Product.Price,
                    Quantity = cartItem.Quantity,
                    LineTotal = cartItem.Product.Price * cartItem.Quantity
                };

                order.Items.Add(orderItem);

            }

            order.TotalAmount = order.Items.Sum(orderItem => orderItem.LineTotal);

            _dbContext.Orders.Add(order);
            _dbContext.CartItems.RemoveRange(cartItems);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(ToOrderResponse(order));
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            return userId;
        }

        private static OrderResponse ToOrderResponse(Order order)
        {
            return new OrderResponse(
                order.Id,
                order.TotalAmount,
                order.Status,
                order.CreatedAtUtc,
                order.Items
                    .Select(orderItem => new OrderItemResponse(
                        orderItem.Id,
                        orderItem.ProductId,
                        orderItem.ProductName,
                        orderItem.ProductSku,
                        orderItem.UnitPrice,
                        orderItem.Quantity,
                        orderItem.LineTotal))
                    .ToList());
        }
    }
}
