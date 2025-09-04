using FrageFejden;
using FrageFejden.Api.Auth;
using FrageFejden.Data;
using FrageFejden.Entities;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default");
if (connectionString is null)
{
    throw new InvalidOperationException("Connection string 'Default' not found.");
}

try
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    Console.WriteLine("✅ Database connection successful!");
    Console.WriteLine(connectionString);
    connection.Close();
}
catch (Exception ex)
{
    Console.WriteLine("❌ Database connection failed: " + ex.Message);
}

// EF Core
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// (Optional) connectivity check
using (SqlConnection connection = new SqlConnection(connectionString))
{
    try
    {
        connection.Open();
    }
    catch (SqlException)
    {

    }
}

// Identity
builder.Services.AddIdentityCore<AppUser>(opt =>
{
    opt.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// JWT Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero, // tighter expiry handling
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });

builder.Services.AddAuthorization();

// Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Enable JWT in Swagger UI
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FrageFejden API", Version = "v1" });
    c.EnableAnnotations();
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {your JWT token}'"
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { scheme, Array.Empty<string>() }
    });
    c.CustomSchemaIds(t =>
{
    var ns = t.Namespace?.Split('.').LastOrDefault();
    return string.IsNullOrWhiteSpace(ns) ? t.Name : $"{ns}.{t.Name}";
});

});

// Jwt token factory
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<DatabaseSeeder>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.Services.SeedDatabaseAsync();


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var r in new[] { "Student", "Lärare", "Admin" })
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole<Guid>(r));
}
app.UseDeveloperExceptionPage();

app.Run();
