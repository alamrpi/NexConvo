using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexConvo.Identity.Api.Common;
using NexConvo.Identity.Application.Authentication.EmailVerification;
using NexConvo.Identity.Application.Authentication.GetCurrentUser;
using NexConvo.Identity.Application.Authentication.Login;
using NexConvo.Identity.Application.Authentication.PasswordReset;
using NexConvo.Identity.Application.Authentication.Refresh;
using NexConvo.Identity.Application.Authentication.Revoke;
using NexConvo.Identity.Application.Authentication.Signup;

namespace NexConvo.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest body, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SignupCommand(body.TenantName, body.TenantSlug, body.Email, body.Password, body.FullName),
            cancellationToken);

        return result.IsSuccess
            ? Created("/api/v1/auth/me", result.Value)
            : result.ToActionResult();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new LoginCommand(body.TenantSlug, body.Email, body.Password), cancellationToken))
            .ToActionResult();

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new RefreshTokenCommand(body.RefreshToken), cancellationToken)).ToActionResult();

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new RevokeTokenCommand(body.RefreshToken), cancellationToken)).ToActionResult();

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId)
            ? (await sender.Send(new GetCurrentUserQuery(userId), cancellationToken)).ToActionResult()
            : Unauthorized();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new VerifyEmailCommand(body.Token), cancellationToken)).ToActionResult();

    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken cancellationToken)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var userId)
            ? (await sender.Send(new ResendVerificationCommand(userId), cancellationToken)).ToActionResult()
            : Unauthorized();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new ForgotPasswordCommand(body.TenantSlug, body.Email), cancellationToken)).ToActionResult();

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken cancellationToken) =>
        (await sender.Send(new ResetPasswordCommand(body.Token, body.NewPassword), cancellationToken)).ToActionResult();
}

public sealed record SignupRequest(
    string TenantName, string TenantSlug, string Email, string Password, string FullName);

public sealed record LoginRequest(string TenantSlug, string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record VerifyEmailRequest(string Token);

public sealed record ForgotPasswordRequest(string TenantSlug, string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);
