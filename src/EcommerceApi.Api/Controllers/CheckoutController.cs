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
        public async Task<ActionResult<OrderResponse>> Checkout(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var cartItems = await _dbContext.CartItems
                .Include(cartItem => cartItem.Product)
                .Where(cartItem => cartItem.UserId == userId.Value)
                .ToListAsync(cancellationToken);

            if (cartItems.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Cart is empty."
                });
            }

            foreach (var cartItem in cartItems)
            {
                if (!cartItem.Product.IsActive)
                {
                    return Conflict(new
                    {
                        message = $"Product '{cartItem.Product.Name}' is no longer available."
                    });
                }

                if (cartItem.Quantity > cartItem.Product.StockQuantity)
                {
                    return Conflict(new
                    {
                        message = $"Not enough stock for product '{cartItem.Product.Name}'."
                    });
                }
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
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

                cartItem.Product.StockQuantity -= cartItem.Quantity;
            }

            order.TotalAmount = order.Items.Sum(orderItem => orderItem.LineTotal);

            _dbContext.Orders.Add(order);
            _dbContext.CartItems.RemoveRange(cartItems);

            await _dbContext.SaveChangesAsync(cancellationToken);

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