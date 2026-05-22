using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.API.Controllers;

/// <summary>Manages the authenticated user's own profile and credentials.</summary>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<UpdateProfileRequest> _updateProfileValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public UsersController(
        IAuthService authService,
        IValidator<UpdateProfileRequest> updateProfileValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _authService             = authService;
        _updateProfileValidator  = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    /// <summary>Updates the authenticated user's first name, last name, and/or email.</summary>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="400">Validation error or no fields provided.</response>
    /// <response code="409">Email is already in use by another account.</response>
    [HttpPatch("me")]
    [ProducesResponseType(typeof(UpdateProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _updateProfileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var result = await _authService.UpdateProfileAsync(GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Changes the authenticated user's password.</summary>
    /// <response code="204">Password changed successfully.</response>
    /// <response code="400">Validation error or current password is incorrect.</response>
    [HttpPatch("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        await _authService.ChangePasswordAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID claim is missing from token."));
}
