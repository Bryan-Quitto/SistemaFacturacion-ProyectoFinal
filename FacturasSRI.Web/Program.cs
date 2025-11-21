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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddMemoryCache();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

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

builder.Services.AddScoped<FacturasSRI.Core.Services.FirmaDigitalService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.XmlGeneratorService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.SriApiClientService>();
builder.Services.AddScoped<FacturasSRI.Core.Services.SriResponseParserService>();

builder.Services.AddScoped<FacturasSRI.Infrastructure.Services.PdfGeneratorService>();

builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("VendedorPolicy", policy => policy.RequireRole("Vendedor", "Administrador"));
    options.AddPolicy("BodegueroPolicy", policy => policy.RequireRole("Bodeguero", "Administrador"));
});

builder.Services.AddHostedService<VencimientoComprasService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

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