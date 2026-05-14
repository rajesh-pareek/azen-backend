using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Azen.Api.Filters;
using Azen.Application.Validation.Auth;
using Azen.Infrastructure;
using Azen.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(opts =>
{
    // Runs FluentValidation on every action argument and converts failures
    // into the standard 400 error envelope.
    opts.Filters.Add<ValidateModelFilter>();
});
builder.Services.AddInfrastructure(builder.Configuration);

// Register every IValidator<T> from the Application assembly (Auth + App validators)
// and from the Api assembly (UploadDocumentRequestValidator).
builder.Services.AddValidatorsFromAssemblyContaining<SendOtpRequestValidator>();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// CORS: which front-end origins are allowed to hit this API.
// The list comes from configuration (appsettings.json + env var overrides).
const string CorsPolicy = "Default";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your Jwt token",
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.MapInboundClaims = false;
        var jwtSecret = builder.Configuration["Jwt:Secret"]!;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var app = builder.Build();

// Auto-apply EF migrations in Development so a fresh `docker compose up`
// (or a new dev's first run) doesn't need a manual dotnet-ef step.
// Production should run migrations explicitly through a deployment pipeline,
// not at app startup - that's why this is gated on IsDevelopment().
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var authDb = services.GetRequiredService<AuthDbContext>();
    var appDb = services.GetRequiredService<AppDbContext>();

    authDb.Database.Migrate();
    appDb.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<Azen.Api.Middlewares.ErrorHandlingMiddleware>();
app.UseHttpsRedirection();

// CORS must run BEFORE authentication so preflight (OPTIONS) requests
// from the browser are answered without needing a JWT.
app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
