using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace CurrencyConverter.API.Controllers;

/// <summary>
/// Authentication controller for generating JWT tokens.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Request a JWT token for authentication.
    /// </summary>
    /// <remarks>Use Test Data:  Username: test, password: password. Role: "User" or "Admin"</remarks>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        // Simple validation (replace with proper identity provider in production)
        if (request.Username != "test" || request.Password != "password")
            return Unauthorized("Invalid credentials");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, request.Username),
            new Claim(ClaimTypes.Role, request.Role) // "User" or "Admin"
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        _logger.LogInformation("Auth Token Created for {UserName} with Role {Role} from ClientIp {IP}",
            request.Username, request.Role, Request.HttpContext.Connection.RemoteIpAddress);
        return Ok(new { Token = new JwtSecurityTokenHandler().WriteToken(token) });
    }
}

public record TokenRequest(string Username, string Password, string Role);