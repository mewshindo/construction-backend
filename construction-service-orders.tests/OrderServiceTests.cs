using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConstructionServiceOrders.Tests;

public class OrderServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _authToken;
    private Guid _userId;

    public OrderServiceTests(WebApplicationFactory<Program> factory)
    {
        // Create a factory with custom authentication configuration
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Override JWT settings for testing
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "construction-users-service",
                        ValidAudience = "construction-api",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret"))
                    };
                });
            });
        });

        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private Task AuthenticateAsync()
    {
        // Create a test user ID
        _userId = Guid.NewGuid();

        // Create a test token
        var token = CreateTestJwtToken(_userId);
        _authToken = token;

        // Set the auth token for subsequent requests
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _authToken);

        return Task.CompletedTask;
    }

    private string CreateTestJwtToken(Guid userId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, $"test.{userId}@example.com"),
            new Claim("name", "Test User"),
            new Claim(ClaimTypes.Role, "user")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "construction-users-service",
            audience: "construction-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldCreateOrderAndReturnSuccess()
    {
        // Arrange
        await AuthenticateAsync();
        var orderData = new CreateOrderRequest
        {
            Title = "Test Order",
            Description = "This is a test order",
            Location = "123 Test St",
            StartDate = DateTime.UtcNow.AddDays(1),
            EstimatedDuration = TimeSpan.FromDays(5)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/orders", orderData);
        var order = await response.Content.ReadFromJsonAsync<Order>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(order);
        Assert.Equal(orderData.Title, order.Title);
        Assert.Equal(orderData.Description, order.Description);
        Assert.Equal(orderData.Location, order.Location);
        Assert.Equal(orderData.StartDate, order.StartDate);
        Assert.Equal(orderData.EstimatedDuration, order.EstimatedDuration);
        Assert.Equal("created", order.Status);
        Assert.Equal(_userId, order.UserId);
    }

    [Fact]
    public async Task GetOrder_AsOwner_ShouldReturnOrder()
    {
        // Arrange
        await AuthenticateAsync();
        
        // Create an order first
        var orderData = new CreateOrderRequest
        {
            Title = "Test Order for Retrieval",
            Description = "This order will be retrieved",
            Location = "123 Test St",
            StartDate = DateTime.UtcNow.AddDays(1),
            EstimatedDuration = TimeSpan.FromDays(5)
        };
        
        var createResponse = await _client.PostAsJsonAsync("/orders", orderData);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);

        // Act
        var response = await _client.GetAsync($"/orders/{createdOrder.Id}");
        var order = await response.Content.ReadFromJsonAsync<Order>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(order);
        Assert.Equal(createdOrder.Id, order.Id);
        Assert.Equal(orderData.Title, order.Title);
        Assert.Equal(_userId, order.UserId);
    }

    [Fact]
    public async Task UpdateOrder_AsNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        // Create first user and their order
        await AuthenticateAsync();
        var orderData = new CreateOrderRequest
        {
            Title = "Original Order",
            Description = "This order will be attempted to be modified by another user",
            Location = "123 Test St",
            StartDate = DateTime.UtcNow.AddDays(1),
            EstimatedDuration = TimeSpan.FromDays(5)
        };
        
        var createResponse = await _client.PostAsJsonAsync("/orders", orderData);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);

        // Create second user and try to update the first user's order
        _client.DefaultRequestHeaders.Authorization = null; // Clear previous auth
        await AuthenticateAsync(); // Create new user and get their token

        var updateData = new UpdateOrderRequest
        {
            Title = "Attempted Update",
            Description = "This update should be rejected",
            Status = "in_progress"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/orders/{createdOrder.Id}", updateData);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_AsOwner_ShouldSucceed()
    {
        // Arrange
        await AuthenticateAsync();
        
        // Create an order first
        var orderData = new CreateOrderRequest
        {
            Title = "Order to Cancel",
            Description = "This order will be cancelled",
            Location = "123 Test St",
            StartDate = DateTime.UtcNow.AddDays(1),
            EstimatedDuration = TimeSpan.FromDays(5)
        };
        
        var createResponse = await _client.PostAsJsonAsync("/orders", orderData);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);

        // Act
        var response = await _client.PostAsync($"/orders/{createdOrder.Id}/cancel", null);
        var updatedOrder = await response.Content.ReadFromJsonAsync<Order>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(updatedOrder);
        Assert.Equal("cancelled", updatedOrder.Status);
        Assert.Equal(createdOrder.Id, updatedOrder.Id);
    }

    private class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new();
    }

    private class UserInfo
    {
        public Guid UUID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    private class CreateOrderRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    private class UpdateOrderRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime? StartDate { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }
        public string? Status { get; set; }
    }

    private class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
    }
}