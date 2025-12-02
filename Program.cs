using System.Text;
using DktApi.Endpoints;
using DktApi.Models.Db;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 🔹 DB
var connStr = builder.Configuration.GetConnectionString("Pg");
if (string.IsNullOrWhiteSpace(connStr))
{
    throw new InvalidOperationException("Connection string 'Pg' not found or empty!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// 🔹 Auth (JWT)
var jwtConfig = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtConfig["Key"]!);

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
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 🔹 Migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ENDPOINTLERİ MAPLE
app.MapAuthEndpoints();
app.MapTherapistEndpoints();
app.MapPlayerEndpoints();
//app.MapTaskEndpoints();
app.MapGameSessionEndpoints();
app.MapLookupEndpoints();
app.MapDashboardEndpoints();

app.Run();
