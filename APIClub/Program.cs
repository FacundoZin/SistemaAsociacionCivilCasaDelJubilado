using APIClub.Domain.AlquilerArticulos.Repositories;
using APIClub.Domain.GestionSocios;
using APIClub.Domain.GestionSocios.Repositories;
using APIClub.Domain.ReservasSalones.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using APIClub.Domain.PaymentsOnline.Repository;
using APIClub.Domain.GestionSocios.Validations;
using APIClub.Infrastructure;
using APIClub.Infrastructure.Persistence.Repositorio;
using APIClub.Infrastructure.Persistence.Data;
using APIClub.Application.Services;
using APIClub.Application.Common;
using APIClub.Application.Validators;
using APIClub.Domain.Auth.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using APIClub.Domain.Notificaciones.Infra;
using APIClub.Domain.Notificaciones.Models;
using APIClub.Domain.Notificaciones.Services;
using Quartz;
using APIClub.Infrastructure.JobsProgramados;
using APIClub.Domain.Analiticas;
using Microsoft.AspNetCore.HttpOverrides;
using APIClub.Domain.AlquilerArticulos.UseCases;
using APIClub.Domain.ModuloGestionCobradores.Repositorios;
using APIClub.Domain.ModuloGestionCobradores.UseCases;
using APIClub.Domain.ModuloGestionCuotas.UseCases;
using APIClub.Domain.ModuloGestionCuotas.Validations;
using APIClub.Domain.PaymentsOnline.ExternalServices;
using APIClub.Domain.PaymentsOnline.useCases;
using APIClub.Domain.Auth.useCases;
using APIClub.Domain.ModuloReservasSalones.useCases;
using APIClub.Infrastructure.Interfaces;
using APIClub.Domain.AlquilerArticulos;
using APIClub.Domain.ModuloGestionViajes.useCases;
using APIClub.Domain.ModuloGestionViajes.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configurar licencia de QuestPDF
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Configuración para PostgreSQL: permitir DateTime sin especificar UTC explícitamente
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddDbContext<AppDbcontext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar WhatsApp
builder.Services.Configure<WhatsAppConfig>(
    builder.Configuration.GetSection("WhatsApp"));

// Registrar HttpClients
builder.Services.AddHttpClient<IWhatsappService, WhatsapService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    client.BaseAddress = new Uri("https://graph.facebook.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", config["WhatsApp:AccessToken"]);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient<IMercadoPagoService, MPService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    client.BaseAddress = new Uri("https://api.mercadopago.com");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue(
            "Bearer",
            config["MercadoPago:AccessToken"]
        );
});

// registrar servicios
builder.Services.AddScoped<ISociosManagmentService, SociosManagmentService>();
builder.Services.AddScoped<ICuotasService, CuotasService>();
builder.Services.AddScoped<IReservasServices, ReservasServices>();
builder.Services.AddScoped<ICobranzasServices, CobranzasService>();
builder.Services.AddScoped<IPdfPlanillaCobranzaService, PdfPlanillaCobranzaService>();
builder.Services.AddScoped<IManagmentArticulosService, ManagmentArticulosService>();
builder.Services.AddScoped<IAlquilerArticulosManagmentService, AlquilerArticulosService>();
builder.Services.AddScoped<IConsultaAlquileres, AlquilerArticulosService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentTokenService, PaymentTokenService>();
builder.Services.AddScoped<IMercadoPagoService, MPService>();
builder.Services.AddScoped<INotificationsService, NotificacionsService>();
builder.Services.AddScoped<IAnaliticasService, AnaliticasService>();
builder.Services.AddScoped<IViajesServices, ViajesService>();



// AUTENTICACIÓN
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsuariosService, UsuariosService>();


//OTROS
builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddScoped<ISocioIntegrityValidator, SocioIntegrityValidator>();
builder.Services.AddScoped<IPagoCuotaValidator, PagoCuotaValidator>();
builder.Services.AddScoped<IDataSeeder, DatabaseSeeder>();


// registrar repositorios
builder.Services.AddScoped<ISocioRepository, SociosRepository>();
builder.Services.AddScoped<ICuotaRepository, CuotaRepository>();
builder.Services.AddScoped<IReservasRepository, ReservasRepository>();
builder.Services.AddScoped<IArticuloRepository, ArticuloRepository>();
builder.Services.AddScoped<IAlquilerRepository, AlquilerRepository>();
builder.Services.AddScoped<IitemAlquilerRepository, ItemsAlquilerRepository>();
builder.Services.AddScoped<IPaymentTokenRepository, PaymentTokenRepository>();
builder.Services.AddScoped<IHistorialCobradoresRepository, HistorialCobradoresRepository>();
builder.Services.AddScoped<IUsuariosRepository, UsuariosRepository>();
builder.Services.AddScoped<IAnaliticasRepository, AnaliticasRepository>();
builder.Services.AddScoped<IViajeReadRepository, ViajeReadRepository>();
builder.Services.AddScoped<IViajeWriteRepository, ViajeWriteRepository>();


builder.Services.AddQuartz(q =>
{
    // JOB 1: Crear tokens
    var createTokensJobKey = new JobKey(
        "CreatePaymentTokensJob",
        "Payments"
    );

    q.AddJob<CreatePaymentTokensJob>(opts =>
        opts.WithIdentity(createTokensJobKey)
    );

    q.AddTrigger(opts => opts
        .ForJob(createTokensJobKey)
        .WithIdentity("CreateTokens_Jan_Jul")
        .WithCronSchedule("0 0 0 1 1,7 ?")
    );

    // JOB 2: Enviar notificaciones
    var notifyJobKey = new JobKey(
        "SendWhatsappPaymentNotificacionJob",
        "Notifications"
    );

    q.AddJob<SendWhatsappPaymentNotificacionJob>(opts =>
        opts.WithIdentity(notifyJobKey)
    );

    q.AddTrigger(opts => opts
        .ForJob(notifyJobKey)
        .WithIdentity("NotifyPayment_Jan_Jul")
        .WithCronSchedule("0 0 7 2 1,7 ?")
    );

    // hello world job de prueba
    //var helloJobKey = new JobKey("HelloWorldJob", "Test");

    //q.AddJob<HelloWorldJob>(opts =>
    //    opts.WithIdentity(helloJobKey)
    //);

    //q.AddTrigger(opts => opts
    //    .ForJob(helloJobKey)
    //    .WithIdentity("HelloWorld_Every20Seconds")
    //    .WithSimpleSchedule(x => x
    //        .WithIntervalInSeconds(20)
    //        .RepeatForever()
    //    )
    //);
});

builder.Services.AddQuartzHostedService(q =>
{
    q.WaitForJobsToComplete = true;
});


// Configuración de JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };

        // Evento para leer el token desde la cookie
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["auth_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// confiugracion cors.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("https://admin.asociacioncivilcasadeljubilado.com.ar", "http://localhost:5173", "http://localhost:5174")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Permitir cookies
        });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


//FLAG PARA CORRES SEEDER
if (args.Contains("--ExecuteSeeder"))
{
    Console.WriteLine(">>> SEEDER FLAG DETECTED: --ExecuteSeeder");
    using var scope = app.Services.CreateScope();

    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.seedTestDataAsync();

    Console.WriteLine(">>> SEEDER PROCESS FINISHED.");
    return;
}

//FLAG PARA CARGAR USARIOS EXISTENTES
if (args.Contains("--ExecuteSeedSociosExisting"))
{
    Console.WriteLine(">>> SEEDER FLAG DETECTED: --ExecuteSeeder");
    using var scope = app.Services.CreateScope();

    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.seedSociosExistentes();

    Console.WriteLine(">>> SEEDER PROCESS FINISHED.");
    return;
}

//FLAG PARA CARGAR VIAJES E INSCRIPTOS
if (args.Contains("--ExecuteSeedViajes"))
{
    Console.WriteLine(">>> SEEDER FLAG DETECTED: --ExecuteSeedViajes");
    using var scope = app.Services.CreateScope();

    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.seedViajesAsync();

    Console.WriteLine(">>> SEEDER PROCESS FINISHED.");
    return;
}

// Aplicar migraciones automáticamente
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbcontext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al aplicar las migraciones.");
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});
// app.UseHttpsRedirection();

// Usar la política CORS que permite credenciales
app.UseCors("AllowFrontend");

app.UseAuthentication(); // AGREGAR ESTO ANTES DE AUTHORIZATION
app.UseAuthorization();

app.MapControllers();

app.Run();
