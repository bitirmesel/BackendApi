using System.Text;
using DktApi.Endpoints;
using DktApi.Models.Db;
using DktApi.Services; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DktApi.Repositories;
using DktApi;



var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. YAPILANDIRMA / SIRLAR
// =========================================================================
// Sırlar repoda tutulmaz. Kaynaklar (sonraki öncekini ezer):
//   appsettings.json → appsettings.{Env}.json → user-secrets (yalnızca Development) → ortam değişkenleri
// Gerekli ortam değişkenleri için README.md'ye bakın.
Console.WriteLine($"[STARTUP] Uygulama başlatılıyor... Ortam: {builder.Environment.EnvironmentName}");

// Tek kaynaktan oku, aynı örneği DI'a ver (controller IOptions<FluentMeOptions> ile alır)
var fluentMe = builder.Configuration.GetSection(FluentMeOptions.SectionName).Get<FluentMeOptions>() ?? new FluentMeOptions();
builder.Services.AddSingleton(Options.Create(fluentMe));
if (!fluentMe.IsConfigured)
{
    // Uygulamayı durdurmuyoruz: yalnızca /api/pronunciation/check etkilenir.
    Console.WriteLine("[WARN] FluentMe ayarları eksik (FluentMe__User, FluentMe__Password, FluentMe__ApiKey). Kelime telaffuz kontrolü 503 dönecek.");
}
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
        throw new InvalidOperationException("Veritabanı bağlantısı bulunamadı! 'ConnectionStrings__Pg' ortam değişkenini (yerelde user-secrets) ayarlayın.");
}

// Render'ın verdiği URL biçimi (postgres://user:pass@host:port/db) Npgsql'de çalışmaz → anahtar=değer biçimine çevir
if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
    connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    var dbUri = new Uri(connStr);
    var userInfo = dbUri.UserInfo.Split(':', 2);
    connStr = new NpgsqlConnectionStringBuilder
    {
        Host = dbUri.Host,
        Port = dbUri.Port > 0 ? dbUri.Port : 5432,
        Database = Uri.UnescapeDataString(dbUri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = SslMode.Require
    }.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// --------------------------------------------------------
// 3. SERVİS ENJEKSİYONLARI (Dependency Injection)
// --------------------------------------------------------
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<CloudinaryService>();
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<PronunciationScoringService>();
builder.Services.AddScoped<WhisperService>();

// Controller desteği
builder.Services.AddControllers(); 

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFlutter", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
// --------------------------------------------------------
// 4. KİMLİK DOĞRULAMA (JWT Auth)
// --------------------------------------------------------
var jwtConfig = builder.Configuration.GetSection("Jwt");
var keyParam = jwtConfig["Key"];
// Varsayılan anahtar yok: tahmin edilebilir bir anahtarla token imzalamaktansa hiç açılmamak daha güvenli.
// HMAC-SHA256 için en az 32 byte gerekir.
if (string.IsNullOrWhiteSpace(keyParam) || Encoding.UTF8.GetByteCount(keyParam) < 32)
    throw new InvalidOperationException("JWT anahtarı eksik veya 32 karakterden kısa! 'Jwt__Key' ortam değişkenini ayarlayın.");

var keyBytes = Encoding.UTF8.GetBytes(keyParam);

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

app.UseCors("AllowFlutter");

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
app.MapNotificationEndpoints();
app.MapSyllablePronunciationEndpoints();
app.MapAdminUpdateEndpoints();
app.MapPlayerInboxEndpoints();
app.MapInvitationEndpoints();
app.MapConnectionRequestEndpoints();

app.Run();