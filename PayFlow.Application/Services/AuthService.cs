using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PayFlow.Application.DTOs;
using PayFlow.Application.Exceptions;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;

namespace PayFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _configuration  = configuration;
        _logger         = logger;
    }

    /// <inheritdoc />
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Registering new user with email {Email}", request.Email);

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A user with email '{request.Email}' already exists.");

        var user = new User
        {
            Email        = request.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName    = request.FirstName,
            LastName     = request.LastName,
            CreatedAt    = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Email} registered successfully", user.Email);

        return BuildAuthResponse(user);
    }

    /// <inheritdoc />
    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for {Email}", request.Email);

        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);

        // Intentionally vague error — don't reveal whether the email exists
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new Application.Exceptions.ValidationException("Invalid email or password.");

        _logger.LogInformation("User {Email} authenticated successfully", user.Email);

        return BuildAuthResponse(user);
    }

    /// <inheritdoc />
    public async Task<UpdateProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);
            if (taken is not null)
                throw new ConflictException($"Email '{request.Email}' is already in use.");

            user.Email = request.Email.ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            user.FirstName = request.FirstName;

        if (!string.IsNullOrWhiteSpace(request.LastName))
            user.LastName = request.LastName;

        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for user {UserId}", userId);

        return new UpdateProfileResponse
        {
            Id        = user.Id,
            Email     = user.Email,
            FirstName = user.FirstName,
            LastName  = user.LastName,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new Application.Exceptions.ValidationException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt    = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token     = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes());

        return new AuthResponse
        {
            Token     = token,
            ExpiresAt = expiresAt,
            Email     = user.Email,
            FirstName = user.FirstName,
            LastName  = user.LastName
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey   = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             jwtSettings["Issuer"],
            audience:           jwtSettings["Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int GetTokenExpiryMinutes()
    {
        var raw = _configuration["Jwt:ExpiryMinutes"];
        return int.TryParse(raw, out var minutes) ? minutes : 60;
    }
}
