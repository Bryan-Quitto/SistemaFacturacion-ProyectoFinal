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

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITaxService, TaxService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IAjusteInventarioService, AjusteInventarioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>(); // Added
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

var supportedCultures = new[]
{
    new CultureInfo("es-EC") // Changed to es-EC for dollar currency symbol
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-EC"), // Changed to es-EC
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});



app.UseStaticFiles();

app.UseStatusCodePagesWithReExecute("/NotFound"); // Handle 404s by re-executing to Blazor's NotFound route

app.UseRouting(); // Add UseRouting here

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseEndpoints(endpoints => // Wrap mappings in UseEndpoints
{
    endpoints.MapControllers(); // API endpoints

    endpoints.MapRazorComponents<App>() // Blazor components
        .AddInteractiveServerRenderMode();
});
    
    app.Run();