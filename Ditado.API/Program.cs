using System.Reflection;
using System.Text;
using Ditado.Aplicacao.Services;
using Ditado.Infra.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS - TOTALMENTE ABERTO
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger com JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Ditado API", 
        Version = "v1",
        Description = "API para gerenciamento de ditados escolares"
    });
    
    // Usar o diretório do assembly em vez de AppContext.BaseDirectory
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, xmlFile);
    
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
        Console.WriteLine($"XML encontrado: {xmlPath}");
    }
    else
    {
        Console.WriteLine($"XML NÃO encontrado em: {xmlPath}");
        Console.WriteLine($"   Procurando em: {Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}");
        Console.WriteLine($"   Arquivos disponíveis: {string.Join(", ", Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "*.xml"))}");
    }
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Bearer {token}\"",
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
            Array.Empty<string>()
        }
    });
});

// Banco de dados
builder.Services.AddDbContext<DitadoDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("ConexaoPadrao"),
        new MySqlServerVersion(new Version(8, 0, 21))
    ));

// JWT Authentication
var chaveSecreta = builder.Configuration["Jwt:ChaveSecreta"] ?? throw new InvalidOperationException("Jwt:ChaveSecreta não configurada");
var key = Encoding.UTF8.GetBytes(chaveSecreta);

builder.Services.AddAuthentication(options =>
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
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Emissor"],
        ValidAudience = builder.Configuration["Jwt:Audiencia"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<DitadoService>();
builder.Services.AddScoped<TurmaService>();
builder.Services.AddScoped<CategoriaService>();
builder.Services.AddScoped<AlunoService>();
builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<TokenService>();

var app = builder.Build();

// APLICAR MIGRATIONS AUTOMATICAMENTE (APENAS EM PRODUÇÃO - NÃO EM DEVELOPMENT NEM TESTING)
if (!app.Environment.IsDevelopment() && !app.Environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Aplicando migrations...");
            var context = services.GetRequiredService<DitadoDbContext>();
            context.Database.Migrate();
            logger.LogInformation("Migrations aplicadas com sucesso!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao aplicar migrations: {Message}", ex.Message);
            throw;
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
 
}

app.UseSwagger();
app.UseSwaggerUI();

// USAR CORS - DEVE VIR ANTES DE UseAuthentication/UseAuthorization
app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Tornar Program acessível para testes
public partial class Program { }
