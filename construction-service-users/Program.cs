
using ConstructionServiceUsers.Models;
using ConstructionServiceUsers.Helpers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add JWT configuration
builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "construction-users-service",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "construction-api",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-your-256-bit-secret")) // At least 32 characters
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Construction Users Service",
        Version = "v1",
        Description = "Manages user accounts for the construction system."
    });

    // Add JWT Bearer auth to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\n\nExample: \"Bearer eyJhbGciOi...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Users Service v1");
});

// Enable authentication/authorization middleware
app.UseAuthentication();
app.UseAuthorization();

var fakeUsersDb = new Dictionary<Guid, User>();

// Helper function to validate email
bool IsValidEmail(string email)
{
    var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    return regex.IsMatch(email);
}

// Generate JWT Token
string GenerateJwtToken(User user)
{
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.UUID.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new("name", user.Name)
    };
    
    // Add roles to claims
    foreach (var role in user.Roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
        builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-your-256-bit-secret"));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: builder.Configuration["Jwt:Issuer"] ?? "construction-users-service",
        audience: builder.Configuration["Jwt:Audience"] ?? "construction-api",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

// ===== GET ALL USERS =====
app.MapGet("/users", () =>
{
    var users = fakeUsersDb.Values.ToList();
    // Don't return password hashes
    foreach (var user in users)
    {
        user.PasswordHash = "";
    }
    return Results.Ok(users);
}).RequireAuthorization();

// ===== GET USER BY UUID =====
app.MapGet("/users/{uuid}", (Guid uuid) =>
{
    if (fakeUsersDb.TryGetValue(uuid, out var user))
    {
        user.PasswordHash = ""; // Don't return password hash
        return Results.Ok(user);
    }
    return Results.NotFound(new { error = "User not found" });
}).RequireAuthorization();

// ===== CREATE USER =====
app.MapPost("/users", (UserRegistrationDto userData) =>
{
    // Validation
    if (string.IsNullOrEmpty(userData.Email) || !IsValidEmail(userData.Email))
        return Results.BadRequest(new { error = "Invalid email address" });
    
    if (string.IsNullOrEmpty(userData.Password) || userData.Password.Length < 8)
        return Results.BadRequest(new { error = "Password must be at least 8 characters long" });
    
    if (string.IsNullOrEmpty(userData.Name))
        return Results.BadRequest(new { error = "Name is required" });

    if (fakeUsersDb.Values.Any(u => u.Email.Equals(userData.Email, StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest(new { error = "Email already registered" });

    var user = new User
    {
        UUID = Guid.NewGuid(),
        Email = userData.Email,
        Name = userData.Name,
        Roles = userData.Roles,
        PasswordHash = PasswordHelper.HashPassword(userData.Password),
        CreatedDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow
    };

    fakeUsersDb[user.UUID] = user;
    
    // Don't return password hash in response
    user.PasswordHash = "";
    return Results.Created($"/users/{user.UUID}", user);
});

// ===== UPDATE USER =====
app.MapPut("/users/{uuid}", (Guid uuid, UserRegistrationDto userData) =>
{
    if (!fakeUsersDb.TryGetValue(uuid, out var existingUser))
        return Results.NotFound(new { error = "User not found" });

    // Validate email if it's being changed
    if (!string.IsNullOrEmpty(userData.Email) && 
        !userData.Email.Equals(existingUser.Email, StringComparison.OrdinalIgnoreCase))
    {
        if (!IsValidEmail(userData.Email))
            return Results.BadRequest(new { error = "Invalid email address" });
            
        if (fakeUsersDb.Values.Any(u => u.Email.Equals(userData.Email, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest(new { error = "Email already registered" });
            
        existingUser.Email = userData.Email;
    }

    // Update password if provided
    if (!string.IsNullOrEmpty(userData.Password))
    {
        if (userData.Password.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters long" });
        existingUser.PasswordHash = PasswordHelper.HashPassword(userData.Password);
    }

    // Update other fields
    if (!string.IsNullOrEmpty(userData.Name))
        existingUser.Name = userData.Name;
        
    if (userData.Roles != null && userData.Roles.Length > 0)
        existingUser.Roles = userData.Roles;

    existingUser.LastModifiedDate = DateTime.UtcNow;

    // Don't return password hash in response
    var response = new User
    {
        UUID = existingUser.UUID,
        Email = existingUser.Email,
        Name = existingUser.Name,
        Roles = existingUser.Roles,
        CreatedDate = existingUser.CreatedDate,
        LastModifiedDate = existingUser.LastModifiedDate
    };
    
    return Results.Ok(response);
}).RequireAuthorization();

// ===== DELETE USER =====
app.MapDelete("/users/{uuid}", (Guid uuid) =>
{
    if (!fakeUsersDb.TryGetValue(uuid, out var user))
        return Results.NotFound(new { error = "User not found" });

    fakeUsersDb.Remove(uuid);
    return Results.Ok(new { message = "User deleted successfully" });
}).RequireAuthorization();

// ===== LOGIN =====
app.MapPost("/login", (LoginRequest loginData) =>
{
    var user = fakeUsersDb.Values.FirstOrDefault(u => 
        u.Email.Equals(loginData.Email, StringComparison.OrdinalIgnoreCase));

    if (user == null)
        return Results.NotFound(new { error = "User not found" });

    if (!PasswordHelper.VerifyPassword(loginData.Password, user.PasswordHash))
        return Results.BadRequest(new { error = "Invalid password" });

    var token = GenerateJwtToken(user);
    
    // Don't return password hash
    user.PasswordHash = "";
    
    return Results.Ok(new LoginResponse
    {
        Token = token,
        User = user
    });
});

// ===== HEALTH CHECK =====
app.MapGet("/users/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        service = "Users Service",
        timestamp = DateTime.UtcNow.ToString("o")
    });
});

// ===== STATUS CHECK =====
app.MapGet("/users/status", () =>
{
    return Results.Json(new { status = "Users service is running" });
});



app.Run();
