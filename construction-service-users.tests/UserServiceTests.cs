using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConstructionServiceUsers.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ConstructionServiceUsers.Tests;

public class UserServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly UserRegistrationDto _testUser;

    public UserServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Create a consistent test user
        _testUser = new UserRegistrationDto
        {
            Email = $"test.{Guid.NewGuid()}@example.com", // Unique email each time
            Password = "TestPassword123!",
            Name = "Test User",
            Roles = new[] { "user" }
        };
    }

    [Fact]
    public async Task Register_WithValidData_ShouldCreateUserAndReturnSuccess()
    {
        // Arrange
        var userData = new UserRegistrationDto
        {
            Email = "test@example.com",
            Password = "Password123!",
            Name = "Test User",
            Roles = new[] { "user" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users", userData);
        var content = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(content);
        Assert.Equal(userData.Email, content.Email);
        Assert.Equal(userData.Name, content.Name);
        Assert.Equal(userData.Roles, content.Roles);
        Assert.NotEqual(default, content.UUID);
        Assert.Empty(content.PasswordHash); // Password hash should not be returned
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var userData = new UserRegistrationDto
        {
            Email = "duplicate@example.com",
            Password = "Password123!",
            Name = "Test User",
            Roles = new[] { "user" }
        };

        // Act - First registration
        await _client.PostAsJsonAsync("/users", userData);

        // Act - Second registration with same email
        var response = await _client.PostAsJsonAsync("/users", userData);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.True(error.ContainsKey("error"));
        Assert.Equal("Email already registered", error["error"]);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        // Register the test user
        Console.WriteLine($"Registering user with email: {_testUser.Email}, password: {_testUser.Password}");
        var registerResponse = await _client.PostAsJsonAsync("/users", _testUser);
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Registration response: {registerContent}");
        Assert.True(registerResponse.StatusCode == HttpStatusCode.Created,
            $"User registration failed with status code {registerResponse.StatusCode}: {registerContent}");

        Console.WriteLine($"Attempting login with email: {_testUser.Email}, password: {_testUser.Password}");
        var loginRequest = new LoginRequest
        {
            Email = _testUser.Email,
            Password = _testUser.Password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);
        
        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Login response: {content}"); // Add debug output
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Login failed with status code {response.StatusCode}: {content}");
        
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        Assert.NotNull(loginResponse);
        Assert.NotEmpty(loginResponse.Token);
        Assert.NotNull(loginResponse.User);
        Assert.Equal(_testUser.Email, loginResponse.User.Email);
    }

    [Fact]
    public async Task AccessProtectedRoute_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/users");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessProtectedRoute_WithValidToken_ShouldSucceed()
    {
        // Arrange
        // 1. Register a user
        var registerResponse = await _client.PostAsJsonAsync("/users", _testUser);
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.StatusCode == HttpStatusCode.Created,
            $"User registration failed with status code {registerResponse.StatusCode}: {registerContent}");

        // 2. Login to get token
        var loginResponse = await _client.PostAsJsonAsync("/login", new LoginRequest
        {
            Email = _testUser.Email,
            Password = _testUser.Password
        });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);

        // 3. Create a new client with the token
        var authorizedClient = _factory.CreateClient();
        authorizedClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", loginResult?.Token ?? throw new Exception("No token received from login"));
            
        // Debug info
        Console.WriteLine($"Using token: {loginResult.Token}");

        // Act
        var response = await authorizedClient.GetAsync("/users");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var users = await response.Content.ReadFromJsonAsync<List<User>>();
        Assert.NotNull(users);
        Assert.Contains(users, u => u.Email == _testUser.Email);
    }
}