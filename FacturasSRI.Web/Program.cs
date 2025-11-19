using FacturasSRI.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Services;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FacturasSRI.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using FacturasSRI.Web;
using SendGrid.Extensions.DependencyInjection;
using Supabase;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(ValidationMessages));
    });

builder.Services.AddDbContext<FacturasSRIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSendGrid(options => {
    options.ApiKey = builder.Configuration.GetSection("SendGrid").GetValue<string>("ApiKey");
});

builder.Services.AddSingleton(provider =>
{
    var url = builder.Configuration["Supabase:Url"];
    var key = builder.Configuration["Supabase:ServiceRoleKey"];
    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
    {
        throw new InvalidOperationException("Supabase URL and Service Role Key must be configured in appsettings.");
    }
    return new Supabase.Client(url, key);
});

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITaxService, TaxService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IAjusteInventarioService, AjusteInventarioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IValidationService, ValidationService>();

builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<FacturasSRI.Core.Services.FirmaDigitalService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.XmlGeneratorService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.SriApiClientService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.SriResponseParserService>();

builder.Services.AddHttpClient("ApiClient", (serviceProvider, client) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    if (httpContextAccessor.HttpContext != null)
    {
        var request = httpContextAccessor.HttpContext.Request;
        client.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
    }
});
builder.Services.AddScoped<ApiClient>();


builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("VendedorPolicy", policy => policy.RequireRole("Vendedor", "Administrador"));
    options.AddPolicy("BodegueroPolicy", policy => policy.RequireRole("Bodeguero", "Administrador"));
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var headers = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
    logger.LogWarning(">>>>>> INCOMING REQUEST: {Method} {Path} | Headers: {Headers}",
        context.Request.Method, context.Request.Path, headers);
    await next.Invoke();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

var supportedCultures = new[]
{
    new CultureInfo("es-EC")
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-EC"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseStaticFiles();

app.UseCookiePolicy();

app.UseStatusCodePagesWithReExecute("/NotFound");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var user = context.User;
        var claims = string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}"));

        logger.LogWarning(">>>>>> API AUTH CHECK: IsAuthenticated={IsAuthenticated}, AuthType={AuthType}, Claims: [{Claims}]",
            user.Identity?.IsAuthenticated ?? false,
            user.Identity?.AuthenticationType ?? "null",
            claims);
    }
    await next.Invoke();
});

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/downloads/purchase-receipt/{id}", async (
    Guid id,
    HttpContext httpContext,
    FacturasSRIDbContext dbContext,
    Client supabase,
    ILogger<Program> logger) =>
{
    logger.LogInformation("Descarga de comprobante solicitada desde Minimal API. ID: {Id}", id);
    var cuentaPorPagar = await dbContext.CuentasPorPagar.FirstOrDefaultAsync(c => c.Id == id);

    if (cuentaPorPagar == null || string.IsNullOrEmpty(cuentaPorPagar.ComprobantePath))
    {
        logger.LogWarning("Minimal API: No se encontró la cuenta por pagar o no tiene comprobante. ID: {Id}", id);
        return Results.NotFound("El comprobante no fue encontrado.");
    }

    var user = httpContext.User;
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var isAdmin = user.IsInRole("Administrador");

    if (cuentaPorPagar.UsuarioIdCreador.ToString() != userId && !isAdmin)
    {
        logger.LogWarning("Minimal API: Acceso denegado para descargar el comprobante. Usuario: {UserId}, Creador: {CreatorId}", userId, cuentaPorPagar.UsuarioIdCreador);
        return Results.Forbid();
    }

    try
    {
        logger.LogInformation("Minimal API: Descargando archivo desde Supabase: {Path}", cuentaPorPagar.ComprobantePath);
        var fileBytes = await supabase.Storage
            .From("comprobantes-compra")
            .Download(cuentaPorPagar.ComprobantePath, null);
        
        var fileName = Path.GetFileName(cuentaPorPagar.ComprobantePath);

        return Results.File(fileBytes, "application/pdf", fileDownloadName: fileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Minimal API: Ocurrió un error al intentar descargar el archivo desde Supabase. Path: {Path}", cuentaPorPagar.ComprobantePath);
        return Results.StatusCode(500);
    }
})
.RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Cookies" });


app.Run();

namespace FacturasSRI.Web
{
    static class AntiforgeryExtensions
    {
        public static RouteHandlerBuilder IgnoreAntiforgeryToken(this RouteHandlerBuilder builder)
        {
            return builder.WithMetadata(new IgnoreAntiforgeryTokenAttribute());
        }
    }
}