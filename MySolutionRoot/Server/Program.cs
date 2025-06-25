using Server;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) Добавляем DbContext для работы с пользователями
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2) SignalR
builder.Services.AddSignalR();
builder.Services
  .AddControllers()
  .AddJsonOptions(opts => {
      // Отключить camelCase
      opts.JsonSerializerOptions.PropertyNamingPolicy = null;
  });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Чтобы SignalR принимал токены по URL параметру access_token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/Hub"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

//Время
builder.Services.AddHostedService<GameTurnTimerService>();

// 3) CORS для WebGL
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .SetIsOriginAllowed(_ => true)
         .AllowCredentials()
    );
});

builder.WebHost.UseUrls("http://localhost:5000"); //поменять для докера на http://0.0.0.0:80
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // создаст файл users.db и таблицы
}

// Порядок важен!
app.UseCors("AllowAll");
app.UseRouting();
// (если будете отдавать аватарки из wwwroot)
// app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<MainHub>("/Hub");
    endpoints.MapControllers();
});

app.Run();
