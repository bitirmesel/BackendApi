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

// --------------------------------------------------------
// 1. VERİTABANI BAĞLANTISI (PostgreSQL)
// --------------------------------------------------------
var connStr = builder.Configuration.GetConnectionString("Pg");
if (string.IsNullOrWhiteSpace(connStr))
{
    throw new InvalidOperationException("Connection string 'Pg' not found or empty!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// --------------------------------------------------------
// 2. SERVİS ENJEKSİYONLARI (Dependency Injection)
// --------------------------------------------------------
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<CloudinaryService>();

// !!! KRİTİK EKLEME 1: Controller desteğini açıyoruz !!!
// GameConfigController'ın çalışması için bu satır ŞARTTIR.
builder.Services.AddControllers(); 

// --------------------------------------------------------
// 3. KİMLİK DOĞRULAMA (JWT Auth)
// --------------------------------------------------------
var jwtConfig = builder.Configuration.GetSection("Jwt");
var keyParam = jwtConfig["Key"];
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
// HttpClient servisini sisteme ekler (BUNU EKLEMEZSEN PROJE ÇÖKER)
builder.Services.AddHttpClient(); 
// ----------------------------------
var app = builder.Build();
// HTTP isteklerini HTTPS'e yönlendir
app.UseHttpsRedirection();

// --------------------------------------------------------
// 5. OTOMATİK MIGRATION
// --------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --------------------------------------------------------
// 6. MIDDLEWARE
// --------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------------
// 7. ENDPOINT MAPPING
// --------------------------------------------------------

// !!! KRİTİK EKLEME 2: Controller rotalarını haritalıyoruz !!!
// Tarayıcıdan gelen istekleri GameConfigController'a yönlendirmek için bu ŞARTTIR.
app.MapControllers(); 

// Mevcut Minimal API Endpoints (Bunlar aynen kalıyor)
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