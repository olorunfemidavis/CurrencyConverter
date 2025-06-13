using System.Diagnostics;
using System.Reflection;
using System.Text;
using Asp.Versioning;
using CurrencyConverter.API.Middleware;
using CurrencyConverter.Application.Queries;
using CurrencyConverter.Domain.Interfaces;
using CurrencyConverter.Infrastructure.Caching;
using CurrencyConverter.Infrastructure.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Serilog;

namespace CurrencyConverter.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            //Add OpenTelemetry for tracing
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: "CurrencyConverter.API", serviceVersion: "1.0.0"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                activity.SetStatus(ActivityStatusCode.Error, response.ReasonPhrase);
                            }
                        };
                    })
                    .AddSource("CurrencyConverter.API")
                    .AddZipkinExporter(zipkin =>
                    {
                        zipkin.Endpoint = new Uri(builder.Configuration["Zipkin:Endpoint"] ?? "http://localhost:9411/api/v2/spans");
                    }));

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            });
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty))
                    };
                });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("UserPolicy", policy => policy.RequireRole("User", "Admin"));
                options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
            });
            builder.Services.AddHttpClient<FrankfurterProvider>(client =>
            {
                var baseUrl = builder.Configuration["Frankfurter:BaseUrl"] ?? "https://api.frankfurter.dev/v1/";
                client.BaseAddress = new Uri(baseUrl);
            }).AddStandardResilienceHandler(options =>
            {
                // Customize retry policy
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests);

                // Customize circuit breaker
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);

                // Log retry attempts
                options.Retry.OnRetry = async args =>
                {
                    var loggerFactory = builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<FrankfurterProvider>();
                    logger.LogWarning("Retry {Attempt} after {Delay}ms due to {Reason}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    await Task.CompletedTask;
                };
            });

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration["Redis:ConnectionString"];
                options.InstanceName = "CurrencyConverter:";
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("Default", opt =>
                {
                    opt.PermitLimit = 100;
                    opt.Window = TimeSpan.FromMinutes(1);
                });
            });
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddZipkinExporter());
            builder.Services.AddSerilog((_, lc) => lc
                .WriteTo.Seq(builder.Configuration["Seq:Url"] ?? string.Empty)
                .Enrich.WithClientIp()
                .Enrich.WithCorrelationId());

            //Register Factory and Services
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetLatestRatesQuery).Assembly));
            builder.Services.AddScoped<ICurrencyProvider, FrankfurterProvider>();
            builder.Services.AddSingleton<ICurrencyProviderFactory, CurrencyProviderFactory>();
            builder.Services.AddScoped<ICacheService, RedisCacheService>();
            builder.Services.AddHealthChecks();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(x =>
            {
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                x.IncludeXmlComments(xmlPath);
                x.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token in the text input.\nExample: \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
                });
                x.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,
                        },
                        new List<string>()
                    }
                });
            });
            var app = builder.Build();

            //Configure Middlewares

            // Register the ResponseLoggingMiddleware
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.MapHealthChecks("/health");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}