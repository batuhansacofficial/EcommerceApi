using EcommerceApi.Api.Contracts.Products;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using EcommerceApi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public sealed class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public ProductsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(
            [FromQuery] ProductSearchRequest request,
            CancellationToken cancellationToken)
        {
            if (request.IsActive == false && !User.IsInRole(UserRoles.Admin))
            {
                return Forbid();
            }

            var query = _dbContext.Products
                .AsNoTracking();

            if (request.MinPrice.HasValue &&
                request.MaxPrice.HasValue &&
                request.MinPrice.Value > request.MaxPrice.Value)
            {
                return BadRequest(new
                {
                    message = "MinPrice cannot be greater than MaxPrice."
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var escaped = request.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                var searchPattern = $"%{escaped}%";

                query = query.Where(product =>
                    EF.Functions.ILike(product.Name, searchPattern, "\\") ||
                    EF.Functions.ILike(product.Description, searchPattern, "\\") ||
                    EF.Functions.ILike(product.Sku, searchPattern, "\\"));
            }

            if (request.MinPrice.HasValue)
            {
                query = query.Where(product => product.Price >= request.MinPrice.Value);
            }

            if (request.MaxPrice.HasValue)
            {
                query = query.Where(product => product.Price <= request.MaxPrice.Value);
            }

            query = query.Where(product => product.IsActive == (request.IsActive ?? true));

            Response.Headers["X-Total-Count"] = (await query.CountAsync(cancellationToken)).ToString();
            var sorted = request.Sort switch
            {
                "low" => query.OrderBy(product => product.Price),
                "high" => query.OrderByDescending(product => product.Price),
                "new" => query.OrderByDescending(product => product.CreatedAtUtc),
                _ => query.OrderBy(product => product.Name)
            };
            var products = await sorted.ThenBy(product => product.Id)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                .Select(product => new ProductResponse(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.StockQuantity,
                    product.IsActive,
                    product.CreatedAtUtc,
                    product.UpdatedAtUtc, product.Version))
                .ToListAsync(cancellationToken);

            return Ok(products);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductResponse>> GetProductById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var canSeeInactive = User.IsInRole(UserRoles.Admin);
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(product => product.Id == id &&
                    (product.IsActive || canSeeInactive))
                .Select(product => new ProductResponse(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.StockQuantity,
                    product.IsActive,
                    product.CreatedAtUtc,
                    product.UpdatedAtUtc, product.Version))
                .FirstOrDefaultAsync(cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost]
        public async Task<ActionResult<ProductResponse>> CreateProduct(
            CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            var skuAlreadyExists = await _dbContext.Products
                .AnyAsync(product => product.Sku == request.Sku, cancellationToken);

            if (skuAlreadyExists)
            {
                return Conflict(new
                {
                    message = $"A product with SKU '{request.Sku}' already exists."
                });
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Sku = request.Sku,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Products.Add(product);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var response = ToProductResponse(product);

            return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, response);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProductResponse>> UpdateProduct(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            var skuAlreadyExists = await _dbContext.Products
                .AnyAsync(existingProduct =>
                    existingProduct.Sku == request.Sku &&
                    existingProduct.Id != id,
                    cancellationToken);

            if (skuAlreadyExists)
            {
                return Conflict(new
                {
                    message = $"A product with SKU '{request.Sku}' already exists."
                });
            }

            var updatedAtUtc = DateTime.UtcNow;
            var updated = await _dbContext.Products
                .Where(existingProduct =>
                    existingProduct.Id == id &&
                    existingProduct.Version == request.Version)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(existingProduct => existingProduct.Name, request.Name)
                    .SetProperty(existingProduct => existingProduct.Description, request.Description)
                    .SetProperty(existingProduct => existingProduct.Sku, request.Sku)
                    .SetProperty(existingProduct => existingProduct.Price, request.Price)
                    .SetProperty(existingProduct => existingProduct.StockQuantity, request.StockQuantity)
                    .SetProperty(existingProduct => existingProduct.IsActive, request.IsActive)
                    .SetProperty(existingProduct => existingProduct.UpdatedAtUtc, updatedAtUtc)
                    .SetProperty(existingProduct => existingProduct.Version, existingProduct => existingProduct.Version + 1),
                    cancellationToken);

            if (updated != 1)
            {
                return Conflict(new { message = "Product changed while editing. Reload and try again." });
            }

            product.Name = request.Name;
            product.Description = request.Description;
            product.Sku = request.Sku;
            product.Price = request.Price;
            product.StockQuantity = request.StockQuantity;
            product.IsActive = request.IsActive;
            product.UpdatedAtUtc = updatedAtUtc;
            product.Version = request.Version!.Value + 1;

            return Ok(ToProductResponse(product));
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProduct(
            Guid id,
            CancellationToken cancellationToken)
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            await _dbContext.Products.Where(p => p.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.IsActive, false)
                .SetProperty(p => p.UpdatedAtUtc, DateTime.UtcNow)
                .SetProperty(p => p.Version, p => p.Version + 1), cancellationToken);

            return NoContent();
        }

        private static ProductResponse ToProductResponse(Product product)
        {
            return new ProductResponse(
                product.Id,
                product.Name,
                product.Description,
                product.Sku,
                product.Price,
                product.StockQuantity,
                product.IsActive,
                product.CreatedAtUtc,
                product.UpdatedAtUtc, product.Version);
        }
    }
}
