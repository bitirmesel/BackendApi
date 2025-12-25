using System.Text;
using DktApi.Endpoints;
using DktApi.Models.Db;
using DktApi.Services; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DktApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. STARTUP DEBUG (HATAYI BURADA YAKALAYACAĞIZ)
// =========================================================================
// Uygulama başlarken Render'daki değişkenleri görüyor mu test ediyoruz.
Console.WriteLine("--------------------------------------------------");
Console.WriteLine("[STARTUP] Uygulama Başlatılıyor...");

var envUser = Environment.GetEnvironmentVariable("FLUENT_USER");
var confUser = builder.Configuration["FLUENT_USER"];

Console.WriteLine($"[ENV CHECK] OS Environment 'FLUENT_USER': '{envUser}'");
Console.WriteLine($"[CONF CHECK] IConfiguration 'FLUENT_USER': '{confUser}'");

if (string.IsNullOrEmpty(confUser))
{
    Console.WriteLine("[CRITICAL WARNING] FLUENT_USER yapılandırmadan okunamadı!");
    Console.WriteLine("Olası Sebepler:");
    Console.WriteLine("1. Render Environment sekmesinde 'Save Changes' yapılmadı.");
    Console.WriteLine("2. Değişken isminde boşluk var (örn: 'FLUENT_USER ').");
    Console.WriteLine("3. appsettings.json içinde bu değer boş string olarak tanımlı ve eziyor.");
}
else
{
    Console.WriteLine("[SUCCESS] FLUENT_USER başarıyla algılandı.");
}
Console.WriteLine("--------------------------------------------------");
// =========================================================================

// --------------------------------------------------------
// 2. VERİTABANI BAĞLANTISI (PostgreSQL)
// --------------------------------------------------------
var connStr = builder.Configuration.GetConnectionString("Pg");
if (string.IsNullOrWhiteSpace(connStr))
{
    // Render bazen connection string'i "DATABASE_URL" olarak verir, onu da kontrol edelim
    connStr = Environment.GetEnvironmentVariable("DATABASE_URL");
    
    if (string.IsNullOrWhiteSpace(connStr))
        throw new InvalidOperationException("Connection string 'Pg' not found or empty!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// --------------------------------------------------------
// 3. SERVİS ENJEKSİYONLARI (Dependency Injection)
// --------------------------------------------------------
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<CloudinaryService>();

// Controller desteği
builder.Services.AddControllers(); 

// --------------------------------------------------------
// 4. KİMLİK DOĞRULAMA (JWT Auth)
// --------------------------------------------------------
var jwtConfig = builder.Configuration.GetSection("Jwt");
var keyParam = jwtConfig["Key"];
// Emniyet sübabı: Eğer key yoksa default kullanmasın, hata versin veya güvenli loglasın
if (string.IsNullOrEmpty(keyParam)) Console.WriteLine("[WARN] JWT Key config'den okunamadı, default değer kullanılabilir.");

var keyBytes = Encoding.UTF8.GetBytes(keyParam ?? "super-secret-key-change-this-32chars-min");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
    });

builder.Services.AddAuthorization();

// --------------------------------------------------------
// 5. SWAGGER / OPENAPI
// --------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

builder.Services.AddHttpClient(); 

// ----------------------------------
var app = builder.Build();

// HTTP isteklerini HTTPS'e yönlendir (Render'da bazen loop yapabilir, dikkat)
// app.UseHttpsRedirection(); // Render zaten https veriyor, bunu şimdilik kapalı tutabilirsin hata alırsan.

// --------------------------------------------------------
// 6. OTOMATİK MIGRATION
// --------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --------------------------------------------------------
// 7. MIDDLEWARE
// --------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------------
// 8. ENDPOINT MAPPING
// --------------------------------------------------------
app.MapControllers(); 

app.MapAuthEndpoints();
app.MapTherapistEndpoints();
app.MapPlayerEndpoints();
app.MapTaskEndpoints();       
app.MapGameSessionEndpoints();
app.MapLookupEndpoints();
app.MapDashboardEndpoints();
app.MapAssetEndpoints();
app.MapMediaEndpoints();

app.Run();