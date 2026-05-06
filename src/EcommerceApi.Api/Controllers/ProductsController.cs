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
                var searchPattern = $"%{request.Search.Trim()}%";

                query = query.Where(product =>
                    EF.Functions.ILike(product.Name, searchPattern) ||
                    EF.Functions.ILike(product.Description, searchPattern) ||
                    EF.Functions.ILike(product.Sku, searchPattern));
            }

            if (request.MinPrice.HasValue)
            {
                query = query.Where(product => product.Price >= request.MinPrice.Value);
            }

            if (request.MaxPrice.HasValue)
            {
                query = query.Where(product => product.Price <= request.MaxPrice.Value);
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(product => product.IsActive == request.IsActive.Value);
            }

            var products = await query
                .OrderBy(product => product.Name)
                .Select(product => new ProductResponse(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.StockQuantity,
                    product.IsActive,
                    product.CreatedAtUtc,
                    product.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Ok(products);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProductResponse>> GetProductById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(product => product.Id == id)
                .Select(product => new ProductResponse(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Sku,
                    product.Price,
                    product.StockQuantity,
                    product.IsActive,
                    product.CreatedAtUtc,
                    product.UpdatedAtUtc))
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

            product.Name = request.Name;
            product.Description = request.Description;
            product.Sku = request.Sku;
            product.Price = request.Price;
            product.StockQuantity = request.StockQuantity;
            product.IsActive = request.IsActive;
            product.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

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

            _dbContext.Products.Remove(product);

            await _dbContext.SaveChangesAsync(cancellationToken);

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
                product.UpdatedAtUtc);
        }
    }
}