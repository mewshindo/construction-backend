
using Polly.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using System.Net.Http.Json;

namespace construction_apigateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            const string USERS_SERVICE_URL = "http://service_users:8000";
            const string ORDERS_SERVICE_URL = "http://service_orders:8000";

            var usersCircuitBreaker = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => (int)msg.StatusCode == 500)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 4,
                    durationOfBreak: TimeSpan.FromSeconds(3),
                    onBreak: (outcome, timespan) =>
                    {
                        Console.WriteLine("Users circuit breaker opened");
                    },
                    onReset: () => Console.WriteLine("Users circuit breaker closed"),
                    onHalfOpen: () => Console.WriteLine("Users circuit breaker half-open")
                );

            var ordersCircuitBreaker = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => (int)msg.StatusCode == 500)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 4,
                    durationOfBreak: TimeSpan.FromSeconds(3),
                    onBreak: (outcome, timespan) =>
                    {
                        Console.WriteLine("Orders circuit breaker opened");
                    },
                    onReset: () => Console.WriteLine("Orders circuit breaker closed"),
                    onHalfOpen: () => Console.WriteLine("Orders circuit breaker half-open")
                );

            builder.Services.AddHttpClient("UsersService", client =>
            {
                client.BaseAddress = new Uri(USERS_SERVICE_URL);
            })
            .AddPolicyHandler(usersCircuitBreaker);

            builder.Services.AddHttpClient("OrdersService", client =>
            {
                client.BaseAddress = new Uri(ORDERS_SERVICE_URL);
            })
            .AddPolicyHandler(ordersCircuitBreaker);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            // ===== USERS ROUTES =====

            app.MapGet("/users/{userId}", async (string userId, IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("UsersService");

                try
                {
                    var response = await client.GetAsync($"/users/{userId}");
                    var data = await response.Content.ReadFromJsonAsync<object>();

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound => Results.NotFound(data),
                        _ => Results.Ok(data)
                    };
                }
                catch
                {
                    return Results.Problem("Users service temporarily unavailable");
                }
            });

            app.MapPost("/users", async (IHttpClientFactory factory, object body) =>
            {
                var client = factory.CreateClient("UsersService");
                try
                {
                    var response = await client.PostAsJsonAsync("/users", body);
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Created("", data);
                }
                catch
                {
                    return Results.Problem("Users service temporarily unavailable");
                }
            });

            app.MapGet("/users", async (IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("UsersService");
                try
                {
                    var data = await client.GetFromJsonAsync<object>("/users");
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Users service temporarily unavailable");
                }
            });

            app.MapDelete("/users/{userId}", async (string userId, IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("UsersService");
                try
                {
                    var response = await client.DeleteAsync($"/users/{userId}");
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Users service temporarily unavailable");
                }
            });

            app.MapPut("/users/{userId}", async (string userId, IHttpClientFactory factory, object body) =>
            {
                var client = factory.CreateClient("UsersService");
                try
                {
                    var response = await client.PutAsJsonAsync($"/users/{userId}", body);
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Users service temporarily unavailable");
                }
            });

            // ===== ORDERS ROUTES =====

            app.MapGet("/orders/{orderId}", async (string orderId, IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("OrdersService");

                try
                {
                    var response = await client.GetAsync($"/orders/{orderId}");
                    var data = await response.Content.ReadFromJsonAsync<object>();

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound => Results.NotFound(data),
                        _ => Results.Ok(data)
                    };
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            app.MapPost("/orders", async (IHttpClientFactory factory, object body) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var response = await client.PostAsJsonAsync("/orders", body);
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Created("", data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            app.MapGet("/orders", async (IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var data = await client.GetFromJsonAsync<List<dynamic>>("/orders");
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            app.MapDelete("/orders/{orderId}", async (string orderId, IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var response = await client.DeleteAsync($"/orders/{orderId}");
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            app.MapPut("/orders/{orderId}", async (string orderId, IHttpClientFactory factory, object body) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var response = await client.PutAsJsonAsync($"/orders/{orderId}", body);
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            // ===== EXTRA ROUTES =====

            app.MapGet("/orders/status", async (IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var data = await client.GetFromJsonAsync<object>("/orders/status");
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            app.MapGet("/orders/health", async (IHttpClientFactory factory) =>
            {
                var client = factory.CreateClient("OrdersService");
                try
                {
                    var data = await client.GetFromJsonAsync<object>("/orders/health");
                    return Results.Ok(data);
                }
                catch
                {
                    return Results.Problem("Orders service temporarily unavailable");
                }
            });

            // ===== AGGREGATION ENDPOINT =====

            app.MapGet("/users/{userId}/details", async (string userId, IHttpClientFactory factory) =>
            {
                var usersClient = factory.CreateClient("UsersService");
                var ordersClient = factory.CreateClient("OrdersService");

                try
                {
                    var userTask = usersClient.GetFromJsonAsync<dynamic>($"/users/{userId}");
                    var ordersTask = ordersClient.GetFromJsonAsync<List<dynamic>>("/orders");

                    await Task.WhenAll(userTask, ordersTask);

                    dynamic user = userTask.Result;

                    if (user is null || (user?.error == "User not found"))
                        return Results.NotFound(new { error = "User not found" });

                    var userOrders = ordersTask.Result.Where(o => (string)o.userId == userId);

                    return Results.Ok(new
                    {
                        user,
                        orders = userOrders
                    });
                }
                catch
                {
                    return Results.Problem("Internal server error");
                }
            });

            // ===== Health Check =====
            app.MapGet("/health", () =>
            {
                return Results.Ok(new
                {
                    status = "API Gateway is running",
                    circuits = new
                    {
                        users = "circuit managed via Polly",
                        orders = "circuit managed via Polly"
                    }
                });
            });

            app.MapGet("/status", () => Results.Ok(new { status = "API Gateway is running" }));

            app.Run();

            app.Run();
        }
    }
}
