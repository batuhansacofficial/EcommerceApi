using System.Security.Claims;
using EcommerceApi.Api.Contracts.Cart;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/cart")]
    public sealed class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CartController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<CartResponse>> GetCart(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var items = await _dbContext.CartItems
                .AsNoTracking()
                .Where(cartItem => cartItem.UserId == userId.Value)
                .OrderBy(cartItem => cartItem.Product.Name)
                .Select(cartItem => new CartItemResponse(
                    cartItem.Id,
                    cartItem.ProductId,
                    cartItem.Product.Name,
                    cartItem.Product.Sku,
                    cartItem.Product.Price,
                    cartItem.Quantity,
                    cartItem.Product.Price * cartItem.Quantity))
                .ToListAsync(cancellationToken);

            var total = items.Sum(item => item.LineTotal);

            return Ok(new CartResponse(items, total));
        }

        [HttpPost("items")]
        public async Task<ActionResult<CartResponse>> AddItem(
            AddCartItemRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            if (request.ProductId == Guid.Empty)
            {
                return BadRequest(new
                {
                    message = "ProductId is required."
                });
            }

            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(product => product.Id == request.ProductId, cancellationToken);

            if (product is null || !product.IsActive)
            {
                return NotFound(new
                {
                    message = "Product was not found."
                });
            }

            if (request.Quantity > product.StockQuantity)
            {
                return Conflict(new
                {
                    message = "Requested quantity exceeds available stock."
                });
            }

            var existingCartItem = await _dbContext.CartItems
                .FirstOrDefaultAsync(
                    cartItem =>
                        cartItem.UserId == userId.Value &&
                        cartItem.ProductId == request.ProductId,
                    cancellationToken);

            if (existingCartItem is null)
            {
                var cartItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _dbContext.CartItems.Add(cartItem);
            }
            else
            {
                var newQuantity = existingCartItem.Quantity + request.Quantity;

                if (newQuantity > 100)
                {
                    return BadRequest(new
                    {
                        message = "Cart item quantity cannot exceed 100."
                    });
                }

                if (newQuantity > product.StockQuantity)
                {
                    return Conflict(new
                    {
                        message = "Requested quantity exceeds available stock."
                    });
                }

                existingCartItem.Quantity = newQuantity;
                existingCartItem.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetCart(cancellationToken);
        }

        [HttpPut("items/{id:guid}")]
        public async Task<ActionResult<CartResponse>> UpdateItem(
            Guid id,
            UpdateCartItemRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var cartItem = await _dbContext.CartItems
                .Include(cartItem => cartItem.Product)
                .FirstOrDefaultAsync(
                    cartItem =>
                        cartItem.Id == id &&
                        cartItem.UserId == userId.Value,
                    cancellationToken);

            if (cartItem is null)
            {
                return NotFound(new
                {
                    message = "Cart item was not found."
                });
            }

            if (request.Quantity > cartItem.Product.StockQuantity)
            {
                return Conflict(new
                {
                    message = "Requested quantity exceeds available stock."
                });
            }

            cartItem.Quantity = request.Quantity;
            cartItem.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetCart(cancellationToken);
        }

        [HttpDelete("items/{id:guid}")]
        public async Task<ActionResult<CartResponse>> RemoveItem(
        Guid id,
        CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();

            if (userId is null)
            {
                return Unauthorized();
            }

            var cartItem = await _dbContext.CartItems
                .FirstOrDefaultAsync(
                    cartItem =>
                        cartItem.Id == id &&
                        cartItem.UserId == userId.Value,
                    cancellationToken);

            if (cartItem is null)
            {
                return NotFound(new
                {
                    message = "Cart item was not found."
                });
            }

            _dbContext.CartItems.Remove(cartItem);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await GetCart(cancellationToken);
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