using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using TaskManager.Data;
using TaskManager.Hubs;
using TaskManager.Services;
using TaskManager.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
{
        options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
});

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Set the Jwt__Secret environment variable.");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = "meridian-taskboard",
            ValidateAudience = true,
            ValidAudience = "meridian-taskboard",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(15)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/taskHub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<TaskStore>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddHostedService<OverdueTaskChecker>();

//Register SignalR with JSON enum string serialization configured
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    }
    );

var app = builder.Build();

// Apply pending EF migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/register", async (RegisterRequest req, TokenService ts) =>
{
    var user = await ts.RegisterAsync(req);
    return user is null
        ? Results.Conflict("Username already taken.")
        : Results.Ok(new { user.Username, user.DisplayName });
});

app.MapPost("/auth/login", async (LoginRequest req, TokenService ts) =>
{
    var result = await ts.AuthenticateAsync(req);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapGet("/api/boards", async (AppDbContext db, TaskStore viewers) =>
{
    var boards = await db.Boards.AsNoTracking().OrderBy(b => b.DisplayName).ToListAsync();
    return Results.Ok(boards.Select(b => new { b.Slug, b.DisplayName, ViewerCount = viewers.GetViewerCount(b.Slug) }));
}).RequireAuthorization();

app.MapPost("/api/boards", async (CreateBoardRequest req, AppDbContext db) =>
{
    var slug = req.Name.Trim().ToLowerInvariant().Replace(" ", "-");
    if (slug.Length < 2 || slug.Length > 60)
        return Results.BadRequest("Board name must be 2–60 characters.");
    if (await db.Boards.AnyAsync(b => b.Slug == slug))
        return Results.Conflict("A board with that name already exists.");

    var board = new Board { Slug = slug, DisplayName = req.Name.Trim(), CreatedAt = DateTime.UtcNow };
    db.Boards.Add(board);
    await db.SaveChangesAsync();
    return Results.Ok(board);
}).RequireAuthorization();

app.MapHub<TaskHub>("/taskHub");

app.Run();

public record CreateBoardRequest(string Name);