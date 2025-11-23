using MHBank.API.Hubs;
using MHBank.API.Middleware;
using MHBank.API.Services;
using MHBank.Core.Interfaces;
using MHBank.Infrastructure.Data;
using MHBank.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// السماح بالاتصال من الـ Emulator والأجهزة الخارجية
builder.WebHost.UseUrls("http://0.0.0.0:5185");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// ═══════════════════════════════════════════════
// إضافة الخدمات
// ═══════════════════════════════════════════════

// قاعدة البيانات
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<TransactionLimitsService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<MHBank.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<FraudDetectionService>();
builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();

// SignalR
builder.Services.AddSignalR();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "MHBank-Super-Secret-Key-Min-32-Chars-For-JWT-2025!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MHBank.API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MHBank.Mobile";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role
    };

    // دعم JWT في SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MH-Bank API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "أدخل JWT Token: Bearer {token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

var app = builder.Build();

// ═══════════════════════════════════════════════
// Middleware Pipeline
// ═══════════════════════════════════════════════

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MH-Bank API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Rate Limiting
app.UseMiddleware<RateLimitMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SignalR Hub
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/", () => new
{
    Message = "🏦 MH-Bank API is Running!",
    Version = "v1.0",
    Swagger = "/swagger",
    SignalR = "/hubs/notifications"
});

app.Run();



