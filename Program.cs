using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySql.EntityFrameworkCore.Extensions;
using projetoX.Config;
using projetoX.Data;
using projetoX.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Garante que as variáveis de ambiente sejam carregadas
builder.Configuration.AddEnvironmentVariables();

// Adiciona IHttpClientFactory para o EnderecoController
builder.Services.AddHttpClient();

// Configurar CORS para produção
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
        {
            var origins =
                Environment
                    .GetEnvironmentVariable("ALLOWED_ORIGINS")
                    ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                ?? new[] { "http://localhost:4200" };

            policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }
    );
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddLogging();

builder.Services.Configure<EmpresaControllerSettings>(
    builder.Configuration.GetSection("EmpresaControllerSettings")
);

// Configuração do TwilioService
var twilioAccountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
var twilioAuthToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
var twilioFromNumber = Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER");
if (string.IsNullOrEmpty(twilioAccountSid))
{
    throw new InvalidOperationException(
        "Twilio AccountSid não configurado corretamente. Verifique as variáveis de ambiente."
    );
}
if (string.IsNullOrEmpty(twilioAuthToken))
{
    throw new InvalidOperationException(
        "Twilio AuthToken não configurado corretamente. Verifique as variáveis de ambiente."
    );
}
if (string.IsNullOrEmpty(twilioFromNumber))
{
    throw new InvalidOperationException(
        "Twilio FromNumber não configurado corretamente. Verifique as variáveis de ambiente."
    );
}
builder.Services.AddSingleton<TwilioService>();

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Obtém a string de conexão da variável de ambiente ou do appsettings.json
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("${CONNECTION_STRING}"))
    {
        throw new InvalidOperationException(
            "Connection string não configurada corretamente. Verifique as variáveis de ambiente ou appsettings.json."
        );
    }
}

// Configuração da conexão com o banco de dados MySQL
builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));

// Configuração da GoogleApiKey como serviço singleton
var googleApiKey = Environment.GetEnvironmentVariable("API_GOOGLE");
if (string.IsNullOrEmpty(googleApiKey))
{
    throw new InvalidOperationException(
        "GoogleApiKey não configurada corretamente. Verifique as variáveis de ambiente."
    );
}
builder.Services.AddSingleton(googleApiKey);

// Configuração de autenticação JWT
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrEmpty(secretKey))
{
    secretKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("${JWT_SECRET_KEY}"))
    {
        throw new InvalidOperationException(
            "JWT SecretKey não configurada corretamente. Verifique as variáveis de ambiente."
        );
    }
}

var key = Encoding.ASCII.GetBytes(secretKey);

var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
if (string.IsNullOrEmpty(issuer))
{
    issuer = builder.Configuration["Jwt:Issuer"];
    if (string.IsNullOrEmpty(issuer) || issuer.Contains("${JWT_ISSUER}"))
    {
        throw new InvalidOperationException(
            "JWT Issuer não configurado corretamente. Verifique as variáveis de ambiente."
        );
    }
}

var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
if (string.IsNullOrEmpty(audience))
{
    audience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrEmpty(audience) || audience.Contains("${JWT_AUDIENCE}"))
    {
        throw new InvalidOperationException(
            "JWT Audience não configurado corretamente. Verifique as variáveis de ambiente."
        );
    }
}

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
        };
    });

// Configuração de autorização
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Adiciona controllers e configurações relacionadas
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Configuração do Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ProjetoX API", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Insira 'Bearer' [espaço] e o token JWT. Exemplo: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
    // Habilita anotações do Swagger
    c.EnableAnnotations();
});

var app = builder.Build();

// Configuração do pipeline
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseSerilogRequestLogging();
app.UseStaticFiles();

app.Run();