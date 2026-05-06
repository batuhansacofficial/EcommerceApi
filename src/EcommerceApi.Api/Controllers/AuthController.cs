using System.Security.Claims;
using EcommerceApi.Api.Contracts.Auth;
using EcommerceApi.Api.Data;
using EcommerceApi.Api.Entities;
using EcommerceApi.Api.Security;
using EcommerceApi.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApi.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthController(
            ApplicationDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            IJwtTokenService jwtTokenService)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(
            RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var emailAlreadyExists = await _dbContext.Users
                .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

            if (emailAlreadyExists)
            {
                return Conflict(new
                {
                    message = $"A user with email '{normalizedEmail}' already exists."
                });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Role = UserRoles.Customer,
                CreatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _dbContext.Users.Add(user);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(CreateAuthResponse(user));
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

            if (user is null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            if (passwordVerificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
                user.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(CreateAuthResponse(user));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<object>> GetMe(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.Role
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return Unauthorized();
            }

            return Ok(user);
        }

        private AuthResponse CreateAuthResponse(User user)
        {
            var accessToken = _jwtTokenService.CreateAccessToken(user);

            return new AuthResponse(
                accessToken,
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role);
        }
    }
}