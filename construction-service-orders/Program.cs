using ConstructionServiceOrders.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// Add Authorization services
builder.Services.AddAuthorization();

// Add JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-your-256-bit-secret"))
        };
    });

builder.Services.AddEndpointsApiExplorer();

// Add support for serving static files
// No need to add static files service in .NET Core

builder.Services.AddSwaggerGen(options =>
{
    // No need to configure SwaggerDoc as we'll use the YAML file directly
});

// Configure Swagger middleware to use the YAML file
builder.Services.ConfigureSwaggerGen(options =>
{
    options.SwaggerDoc("v1", null); // Required for the middleware to work
    var yamlPath = Path.Combine(AppContext.BaseDirectory, "docs", "openapi.ru.yaml");
    if (File.Exists(yamlPath))
    {
        options.CustomSchemaIds(type => type.FullName);
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "construction-service-orders.xml"));
    }

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

// Serve the OpenAPI specification file directly
app.UseStaticFiles();

app.UseSwagger(c =>
{
    c.SerializeAsV2 = false;
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
        };
    });
});

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi.ru.yaml", "API Строительного Сервиса - Заказы");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "API Строительного Сервиса - Документация";
    options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    options.DisplayRequestDuration();
    options.EnableDeepLinking();
    options.EnableFilter();
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
});

// Enable authentication/authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// In-memory database
var ordersDb = new Dictionary<Guid, Order>();

// Helper function to get user ID from JWT token
Guid GetUserIdFromToken(HttpContext context)
{
    // Check both JWT "sub" claim and Windows Identity name (for testing)
    var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)
        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User ID not found in token");
    
    return Guid.Parse(userIdClaim.Value);
}

// Validation helper
bool IsUserAuthorized(HttpContext context, Order order)
{
    var userId = GetUserIdFromToken(context);
    return order.UserId == userId;
}

// ===== CREATE ORDER =====
app.MapPost("/orders", (HttpContext context, CreateOrderRequest request) =>
{
    var userId = GetUserIdFromToken(context);
    
    var order = new Order
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = request.Title,
        Description = request.Description,
        Location = request.Location,
        StartDate = request.StartDate,
        EstimatedDuration = request.EstimatedDuration,
        Status = "created",
        CreatedDate = DateTime.UtcNow,
        LastModifiedDate = DateTime.UtcNow
    };

    ordersDb[order.Id] = order;
    return Results.Created($"/orders/{order.Id}", order);
}).RequireAuthorization();

// ===== GET USER'S ORDERS =====
app.MapGet("/orders", (HttpContext context) =>
{
    var userId = GetUserIdFromToken(context);
    var userOrders = ordersDb.Values.Where(o => o.UserId == userId).ToList();
    return Results.Ok(userOrders);
}).RequireAuthorization();

// ===== GET ORDER BY ID =====
app.MapGet("/orders/{id}", (HttpContext context, Guid id) =>
{
    if (!ordersDb.TryGetValue(id, out var order))
        return Results.NotFound(new { error = "Order not found" });

    if (!IsUserAuthorized(context, order))
        return Results.Forbid();

    return Results.Ok(order);
}).RequireAuthorization();

// ===== UPDATE ORDER =====
app.MapPut("/orders/{id}", (HttpContext context, Guid id, UpdateOrderRequest request) =>
{
    if (!ordersDb.TryGetValue(id, out var order))
        return Results.NotFound(new { error = "Order not found" });

    if (!IsUserAuthorized(context, order))
        return Results.Forbid();

    // Update only provided fields
    if (request.Title != null)
        order.Title = request.Title;
    if (request.Description != null)
        order.Description = request.Description;
    if (request.Location != null)
        order.Location = request.Location;
    if (request.StartDate.HasValue)
        order.StartDate = request.StartDate.Value;
    if (request.EstimatedDuration.HasValue)
        order.EstimatedDuration = request.EstimatedDuration.Value;
    if (request.Status != null)
        order.Status = request.Status;

    order.LastModifiedDate = DateTime.UtcNow;
    return Results.Ok(order);
}).RequireAuthorization();

// ===== CANCEL ORDER =====
app.MapPost("/orders/{id}/cancel", (HttpContext context, Guid id) =>
{
    if (!ordersDb.TryGetValue(id, out var order))
        return Results.NotFound(new { error = "Order not found" });

    if (!IsUserAuthorized(context, order))
        return Results.Forbid();

    order.Status = "cancelled";
    order.LastModifiedDate = DateTime.UtcNow;
    return Results.Ok(order);
}).RequireAuthorization();

// ===== DELETE ORDER =====
app.MapDelete("/orders/{id}", (HttpContext context, Guid id) =>
{
    if (!ordersDb.TryGetValue(id, out var order))
        return Results.NotFound(new { error = "Order not found" });

    if (!IsUserAuthorized(context, order))
        return Results.Forbid();

    ordersDb.Remove(id);
    return Results.Ok(new { message = "Order deleted successfully" });
}).RequireAuthorization();

// ===== STATUS & HEALTH =====
app.MapGet("/orders/status", () =>
{
    return Results.Json(new { status = "Orders service is running" });
});

app.MapGet("/orders/health", () =>
{
    return Results.Json(new
    {
        status = "OK",
        service = "Orders Service",
        timestamp = DateTime.UtcNow.ToString("o")
    });
});

app.Run();

public partial class Program { }
namespace ConstructionServiceOrders { } // Needed for test project to reference
