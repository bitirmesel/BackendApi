using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq; // FirstOrDefault için
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ---------------- DB SERVICE ----------------
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("Pg")));

// ---------------- JWT CONFIG ----------------
var jwtSection  = builder.Configuration.GetSection("Jwt");
var jwtIssuer   = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var jwtKey      = jwtSection["Key"]!;

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer   = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --------- middleware pipeline ----------
app.UseAuthentication();
app.UseAuthorization();

// --------------- BASIC ENDPOINTS ---------------

app.MapGet("/", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health", async (IDbConnection db) =>
{
    if (db is NpgsqlConnection conn) await conn.OpenAsync();
    var ok = await db.ExecuteScalarAsync<int>("SELECT 1");
    return Results.Ok(new { db = ok == 1 ? "up" : "down" });
});

// --------------- GAME SESSION CREATE ---------------

app.MapPost("/api/game-sessions", async (IDbConnection db, CreateGameSessionReq req, ILoggerFactory lf) =>
{
    if (db is NpgsqlConnection conn) await conn.OpenAsync();
    var log = lf.CreateLogger("game-sessions");

    try
    {
        const string sql = @"
            INSERT INTO game_sessions(
                player_id, game_id, letter_id, asset_set_id, task_id,
                score, max_score, started_at
            )
            VALUES (
                @player_id, @game_id, @letter_id, @asset_set_id, @task_id,
                0, @max_score, NOW()
            )
            RETURNING id;";

        long sessionId = await db.ExecuteScalarAsync<long>(sql, new
        {
            player_id    = req.PlayerId,
            game_id      = req.GameId,
            letter_id    = req.LetterId,
            asset_set_id = req.AssetSetId,
            task_id      = req.TaskId,
            max_score    = req.MaxScore ?? 0
        });

        return Results.Ok(new { sessionId });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Error creating game session");
        return Results.Problem(ex.ToString(), statusCode: 500);
    }
});

// --------------- AUTH / LOGIN ---------------

app.MapPost("/api/auth/login", async (IDbConnection db, LoginRequest req) =>
{
    if (db is NpgsqlConnection conn) await conn.OpenAsync();

    string role = (req.Role ?? "").Trim().ToLowerInvariant();
    if (role != "player" && role != "therapist")
        return Results.BadRequest(new { error = "Role must be 'player' or 'therapist'." });

    string tableName = role == "therapist" ? "therapists" : "players";

    string sql = $@"
        SELECT id, name, email, password
        FROM {tableName}
        WHERE email = @email
        LIMIT 1;
    ";

    var rows = await db.QueryAsync<DbUser>(sql, new { email = req.Email });
    var user = rows.FirstOrDefault();

    if (user is null)
        return Results.Unauthorized();

    // şimdilik plain-text
    if (!string.Equals(user.Password, req.Password))
        return Results.Unauthorized();

    string token = GenerateJwtToken(
        userId: user.Id,
        name:   user.Name,
        email:  user.Email,
        role:   role,
        issuer: jwtIssuer,
        audience: jwtAudience,
        signingKey: signingKey
    );

    return Results.Ok(new LoginResponse(
        Token: token,
        Role: role,
        UserId: user.Id,
        Name: user.Name,
        Email: user.Email
    ));
});

// --------------- PROTECTED TEST ENDPOINT ---------------

app.MapGet("/api/me", [Microsoft.AspNetCore.Authorization.Authorize] (ClaimsPrincipal user) =>
{
    var id   = user.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var role = user.FindFirstValue(ClaimTypes.Role);
    var name = user.FindFirstValue("name");

    return Results.Ok(new
    {
        userId = id,
        role,
        name
    });
});

app.Run();

// ---------------- TOKEN FUNCTION (local func) -----------

string GenerateJwtToken(
    long userId,
    string name,
    string email,
    string role,
    string issuer,
    string audience,
    SymmetricSecurityKey signingKey)
{
    var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, email),
        new Claim("name", name),
        new Claim(ClaimTypes.Role, role)
    };

    var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddHours(4),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
