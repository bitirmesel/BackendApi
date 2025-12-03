using System.Text;
using DktApi.Endpoints;
using DktApi.Models.Db;
using DktApi.Services; // CloudinaryService için gerekli
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------
// 1. VERİTABANI BAĞLANTISI (PostgreSQL)
// --------------------------------------------------------
var connStr = builder.Configuration.GetConnectionString("Pg");
if (string.IsNullOrWhiteSpace(connStr))
{
    // Render ortamında bazen connection string environment variable'dan farklı okunabilir,
    // hata alırsan loglara bakmak için bu kontrol önemli.
    throw new InvalidOperationException("Connection string 'Pg' not found or empty!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// --------------------------------------------------------
// 2. SERVİS ENJEKSİYONLARI (Dependency Injection)
// --------------------------------------------------------

// Cloudinary Servisi (Yeni Eklenen)
// Render Environment Variables'dan okuyup çalışacak.
builder.Services.AddScoped<CloudinaryService>();

// --------------------------------------------------------
// 3. KİMLİK DOĞRULAMA (JWT Auth)
// --------------------------------------------------------
var jwtConfig = builder.Configuration.GetSection("Jwt");
var keyParam = jwtConfig["Key"];

// Key null gelirse uygulama patlamasın diye önlem (Local dev için)
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
// 4. SWAGGER / OPENAPI
// --------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Swagger'da kilit ikonunu aktifleştirmek için JWT ayarı
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

var app = builder.Build();

// --------------------------------------------------------
// 5. OTOMATİK MIGRATION (Veritabanı Güncelleme)
// --------------------------------------------------------
// Render her deploy ettiğinde veritabanını güncel tutar.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --------------------------------------------------------
// 6. MIDDLEWARE (Ara Katmanlar)
// --------------------------------------------------------

// Swagger'ı Development dışında da görmek istersen if bloğunu kaldırabilirsin.
// Şimdilik sadece geliştirmede açık kalsın.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------------
// 7. ENDPOINT MAPPING (Yönlendirmeler)
// --------------------------------------------------------

app.MapAuthEndpoints();
app.MapTherapistEndpoints();
app.MapPlayerEndpoints();
app.MapTaskEndpoints();       
app.MapGameSessionEndpoints();
app.MapLookupEndpoints();
app.MapDashboardEndpoints();
app.MapAssetEndpoints();
// YENİ EKLENEN: Medya yükleme endpoint'i
app.MapMediaEndpoints(); 

app.Run();