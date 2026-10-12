using System.Runtime.InteropServices;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NetKubernetes.Data;
using NetKubernetes.Data.Inmuebles;
using NetKubernetes.Data.Usuarios;
using NetKubernetes.Middleware;
using NetKubernetes.Models;
using NetKubernetes.Profiles;
using NetKubernetes.Token;

var builder = WebApplication.CreateBuilder(args);

// La cadena de conexion y la clave del token no viven en el repositorio.
// Se inyectan por variable de entorno (ConnectionStrings__SQLServerConnection,
// Jwt__Key) o por "dotnet user-secrets" en desarrollo. Ver README.
var connectionString = builder.Configuration.GetConnectionString("SQLServerConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta la cadena de conexion 'SQLServerConnection'. Definila con la variable " +
        "de entorno ConnectionStrings__SQLServerConnection o con 'dotnet user-secrets'. " +
        "Ver la seccion de configuracion del README.");
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Falta la clave 'Jwt:Key' o es demasiado corta (minimo 32 caracteres). " +
        "Definila con la variable de entorno Jwt__Key o con 'dotnet user-secrets'. " +
        "Ver la seccion de configuracion del README.");
}

builder.Services.AddDbContext<AppDbContext>(opt => {
    opt.LogTo(Console.WriteLine, new [] {
        DbLoggerCategory.Database.Command.Name},
        LogLevel.Information);

    opt.UseSqlServer(connectionString);
});

// Alternativa con MySQL (proveedor Pomelo). La probé para ver que tan
// intercambiable es el motor en EF Core: el modelo y las entidades no cambian,
// solo el proveedor y la cadena de conexion. Me quedé con SQL Server, pero lo
// dejo como referencia. Para usarla, comenta el AddDbContext de arriba.
// var connectionMySqlString = builder.Configuration.GetConnectionString("MySqlConnection");
// builder.Services.AddDbContext<AppDbContext>(options =>
// {
//     options.UseMySql(connectionMySqlString, ServerVersion.AutoDetect(connectionMySqlString));
// });

builder.Services.AddScoped<IInmuebleRepository,  InmuebleRepository>();


// Add services to the container.

builder.Services.AddControllers( opt => {
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    opt.Filters.Add(new AuthorizeFilter(policy));
});


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt => {
    opt.SwaggerDoc("v1", new OpenApiInfo {
        Title = "NetKubernetes API",
        Version = "v1",
        Description = "API REST de inmuebles con autenticacion JWT, desplegada en Kubernetes."
    });

    // Boton "Authorize" en Swagger UI, para poder probar los endpoints
    // protegidos con el token que devuelve POST /api/usuario/login.
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega aqui el token que devuelve POST /api/usuario/login"
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var mapperConfig = new MapperConfiguration(mc => {
    mc.AddProfile(new InmuebleProfile());
});

IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);


var builderSecurity = builder.Services.AddIdentityCore<Usuario>();
var identityBuilder = new IdentityBuilder(builderSecurity.UserType, builder.Services);
identityBuilder.AddEntityFrameworkStores<AppDbContext>();
identityBuilder.AddSignInManager<SignInManager<Usuario>>();
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddScoped<IJwtGenerador, JwtGenerador>();
builder.Services.AddScoped<IUsuarioSesion, UsuarioSesion>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer( opt => {
                    opt.TokenValidationParameters = new TokenValidationParameters 
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = key,
                        ValidateAudience = false,
                        ValidateIssuer = false
                    };

                });


builder.Services.AddCors( o => o.AddPolicy("corsapp", builder => {
    builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
}));


var app = builder.Build();

// Configure the HTTP request pipeline.
// Swagger queda habilitado tambien fuera de desarrollo: es la forma de
// explorar la API cuando corre en Docker o en Kubernetes. En un proyecto
// real esto iria detras de autenticacion o limitado a entornos internos.
app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ManagerMiddleware>();

app.UseAuthentication();
app.UseCors("corsapp");
//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();


// Aplica las migraciones y siembra datos al arrancar. En Docker y en
// Kubernetes el contenedor de la API suele estar listo antes que SQL Server,
// asi que se reintenta en vez de rendirse en el primer fallo.
using (var ambiente = app.Services.CreateScope())
{
    var services = ambiente.ServiceProvider;
    var logging = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();

    // Solo se reintenta la espera por la base de datos, que si es transitoria:
    // en Docker y en Kubernetes la API suele arrancar antes que SQL Server.
    // 15 x 5s = 75s, suficiente para el primer arranque de un contenedor.
    const int intentosMaximos = 15;
    var esperaEntreIntentos = TimeSpan.FromSeconds(5);
    var disponible = false;

    for (var intento = 1; intento <= intentosMaximos && !disponible; intento++)
    {
        if (await context.Database.CanConnectAsync())
        {
            disponible = true;
            break;
        }

        logging.LogWarning(
            "La base de datos aun no responde (intento {Intento}/{Total}). Reintentando en {Espera}s",
            intento, intentosMaximos, esperaEntreIntentos.TotalSeconds);

        await Task.Delay(esperaEntreIntentos);
    }

    if (!disponible)
    {
        logging.LogError(
            "La base de datos no respondio tras {Total} intentos. La API arranca igual, " +
            "pero fallara en cada peticion que toque datos.", intentosMaximos);
    }
    else
    {
        // Ya hay conexion: los errores de aqui en adelante son deterministas
        // (esquema inconsistente, migracion invalida) y no se arreglan reintentando,
        // asi que se reportan una vez con su causa.
        try {
            var userManager = services.GetRequiredService<UserManager<Usuario>>();
            await context.Database.MigrateAsync();
            await LoadDatabase.InsertarData(context, userManager);

            logging.LogInformation("Migraciones aplicadas correctamente");

        }catch(Exception e) {
            logging.LogError(e, "Ocurrio un error aplicando las migraciones");
        }
    }
}







app.Run();
