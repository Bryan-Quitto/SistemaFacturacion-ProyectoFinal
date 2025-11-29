using FacturasSRI.Web.Components;
using Microsoft.AspNetCore.Components.Web;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Services;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
using FacturasSRI.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using SendGrid.Extensions.DependencyInjection;
using Supabase;
using FacturasSRI.Web.Endpoints;

System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddPooledDbContextFactory<FacturasSRIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(p => 
    p.GetRequiredService<IDbContextFactory<FacturasSRIDbContext>>().CreateDbContext());

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
builder.Services.AddScoped<ICobroService, CobroService>();

builder.Services.AddScoped<FacturasSRI.Core.Services.FirmaDigitalService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.XmlGeneratorService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.SriResponseParserService>();
builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();

builder.Services.AddScoped<FacturasSRI.Infrastructure.Services.PdfGeneratorService>();

builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// This is the correct way to get the base URI for an outgoing HttpClient
// in a server-side Blazor app when you need to call your own API.
builder.Services.AddScoped(sp => 
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor.HttpContext;

    if (httpContext == null)
    {
        throw new InvalidOperationException("No se puede construir la URI base para HttpClient en un contexto sin HTTP.");
    }
    
    var request = httpContext.Request;
    var uriBuilder = new UriBuilder
    {
        Scheme = request.Scheme,
        Host = request.Host.Host,
        Port = request.Host.Port ?? (request.Scheme == "https" ? 443 : 80)
    };
    return new HttpClient { BaseAddress = uriBuilder.Uri };
});

builder.Services.AddHttpClient<FacturasSRI.Core.Services.SriApiClientService>(client => 
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; FacturasSRI/1.0)");
    
    // ESTO ES VITAL: El SRI odia el header "Expect: 100-continue" y te cuelga si lo mandas.
    client.DefaultRequestHeaders.ExpectContinue = false; 
})
.ConfigurePrimaryHttpMessageHandler(() => 
{
    var handler = new HttpClientHandler();
    
    // VITAL: El SRI solo habla TLS 1.2. Si usas 1.3 o SSL3, te cuelga.
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    
    // VITAL: El servidor de pruebas "celcer" a veces tiene certificados caducados. Esto los ignora.
    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

    return handler;
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddCookie("CustomerAuth", options =>
    {
        options.LoginPath = "/portal/login";
        options.AccessDeniedPath = "/portal/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("VendedorPolicy", policy => policy.RequireRole("Vendedor", "Administrador"));
    options.AddPolicy("BodegueroPolicy", policy => policy.RequireRole("Bodeguero", "Administrador"));
    options.AddPolicy("IsCustomer", policy => policy.RequireClaim("UserType", "Cliente").AddAuthenticationSchemes("CustomerAuth"));
});

builder.Services.AddHostedService<VencimientoComprasService>();
builder.Services.AddHostedService<DataCacheService>();

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);

// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
app.UseHsts();

app.UseHttpsRedirection();

var supportedCultures = new[] { new CultureInfo("es-EC") };
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

app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDownloadEndpoints();

app.Run();