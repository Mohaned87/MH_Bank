using Microsoft.EntityFrameworkCore;
using MHBank.Infrastructure.Data;


var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════
// إضافة الخدمات
// ═══════════════════════════════════════════════

// قاعدة البيانات
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MH-Bank API", Version = "v1" });
});

var app = builder.Build();

// ═══════════════════════════════════════════════
// Middleware Pipeline
// ═══════════════════════════════════════════════

// تفعيل Swagger

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MH-Bank API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// الصفحة الرئيسية
app.MapGet("/", () => new
{
    Message = "🏦 MH-Bank API is Running!",
    Version = "v1.0",
    Swagger = "/swagger"
});

app.Run();
