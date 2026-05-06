using System.Security.Claims;
using EcommerceApi.Api.Contracts.Orders;
using EcommerceApi.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/orders")]
    public sealed class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public OrdersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetOrders(
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.UserId == userId.Value)
                .OrderByDescending(order => order.CreatedAtUtc)
                .Select(order => new OrderResponse(
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
                        .ToList()))
                .ToListAsync(cancellationToken);

            return Ok(orders);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<OrderResponse>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var order = await _dbContext.Orders
                .AsNoTracking()
                .Where(order =>
                    order.Id == id &&
                    order.UserId == userId.Value)
                .Select(order => new OrderResponse(
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
                        .ToList()))
                .FirstOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                return NotFound();
            }

            return Ok(order);
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
    }
}