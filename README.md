[![NuGet Downloads](https://img.shields.io/nuget/dt/IdentiPoint.svg)](https://github.com/Ethan0007/IdentiPoint)
[![NuGet Version](https://img.shields.io/nuget/v/IdentiPoint.svg)](https://github.com/Ethan0007/IdentiPoint)
 
 # 🧩 IdentiPoint — Lightweight Identity & Authentication Library for .NET

A lightweight, single-file **Mini Identity Server** designed for .NET 8 applications.  
Provides user registration, login, JWT-based authentication, and refresh tokens — without the complexity of ASP.NET Core Identity.

---

## 🚀 Features

- Simple and minimal identity management
- Secure PBKDF2 password hashing
- JWT access and refresh token generation
- EF Core support (SQL Server, SQLite, or InMemory)
- Easily extendable for your own entities (e.g., profiles, audit logs)

---

## 🆚 MiniIdentity vs ASP.NET Core Identity

MiniIdentity focuses on simplicity and API-first authentication, while ASP.NET Core Identity is a full membership framework.

| Feature | MiniIdentity | ASP.NET Core Identity |
|---|---|---|
| Setup complexity | Minimal | High |
| Designed for APIs | ✅ Yes | ⚠️ Indirect |
| JWT support | Built-in | Requires configuration |
| Database schema | Simple | Complex |
| Extensibility | Native EF Core | Framework-dependent |
| Learning curve | Low | High |
| Dependencies | Lightweight | Heavy |
| Full control over auth flow | ✅ Yes | ⚠️ Partial |
| Built-in UI system | ❌ No | ✅ Yes |
| External providers (Google, etc.) | ❌ No | ✅ Yes |

## ⚙️ Installation

Add Entity Framework Core and SQL Server provider:

```bash
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```
### 📁 Folder Setup Example
/YourProject
 ├── Controllers/
 │    ├── AuthController.cs
 │    └── ProfileController.cs
 ├── Data/
 │    └── CustomIdentityDbContext.cs
 ├── Program.cs
 ├── appsettings.json
 └── README.md

### 🏗️ Program.cs (SQL Server Setup)
```
using IdentiPoint;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ Configure SQL Server Database
builder.Services.AddDbContext<MiniIdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Register MiniIdentity
builder.Services.AddMiniIdentity(options =>
{
    options.JwtSecret = "YourStrongSecretKeyHere_AtLeast32Chars"; // Must be at least 32 chars
    options.Issuer = "IdentiPoint";
    options.Audience = "IdentiPointClients";
});

// ✅ Configure JWT Authentication
var jwtKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "YourStrongSecretKeyHere_AtLeast32Chars");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = "IdentiPoint",
        ValidAudience = "IdentiPointClients"
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```
### ⚙️ appsettings.json
```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MiniIdentityDB;User Id=sa;Password=P@ssword123;TrustServerCertificate=True"
  },
  "Jwt": {
    "Secret": "YourStrongSecretKeyHere_AtLeast32Chars"
  }
}
```
### 👤 AuthController (Register & Login)
```
using IdentiPoint;
using Microsoft.AspNetCore.Mvc;

namespace IdentiPointDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MiniIdentityManager _identity;

        public AuthController(MiniIdentityManager identity)
        {
            _identity = identity;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var (success, message) = await _identity.RegisterAsync(req.Username, req.Email, req.Password);
            if (!success) return BadRequest(new { message });
            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var (ok, token, refresh) = await _identity.LoginAsync(req.UsernameOrEmail, req.Password);
            if (!ok) return Unauthorized(new { message = "Invalid credentials" });
            return Ok(new { token, refresh });
        }
    }

    public record RegisterRequest(string Username, string Email, string Password);
    public record LoginRequest(string UsernameOrEmail, string Password);
}
```
### 🔐 ProfileController (Protected Routes)
```
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentiPointDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🔒 Requires valid JWT
    public class ProfileController : ControllerBase
    {
        [HttpGet("me")]
        public IActionResult GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.Identity?.Name;

            return Ok(new
            {
                message = "Protected route access granted ✅",
                userId,
                username,
                issuedAt = DateTime.UtcNow
            });
        }

        [HttpGet("secure-data")]
        public IActionResult GetSecureData()
        {
            return Ok(new
            {
                secret = "This data is only for authenticated users 🔐",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
```
### 🧩 Extending the DbContext
You can extend MiniIdentityDbContext to include your own entities (e.g., profiles, audit logs).
```
using Microsoft.EntityFrameworkCore;

namespace IdentiPoint
{
    public class CustomIdentityDbContext : MiniIdentityDbContext
    {
        public CustomIdentityDbContext(DbContextOptions<CustomIdentityDbContext> options)
            : base(options) { }

        public DbSet<UserProfile> UserProfiles { get; set; }
    }

    public class UserProfile
    {
        public int Id { get; set; }
        public string UserId { get; set; }  // FK to MiniUser.Id
        public string FullName { get; set; }
        public string Address { get; set; }
    }
}
```
Update registration in Program.cs if using custom context:
```
builder.Services.AddDbContext<CustomIdentityDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```
### 🧱 Optional: Entity Framework Migration
If using SQL Server or another relational DB:
```
dotnet ef migrations add InitialCreate
dotnet ef database update
```


