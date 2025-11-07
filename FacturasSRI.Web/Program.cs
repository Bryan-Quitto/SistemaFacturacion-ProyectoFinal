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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

builder.Services.AddDbContextFactory<FacturasSRIDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .LogTo(Console.WriteLine, LogLevel.Information));

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITaxService, TaxService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();

builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp => 
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    string baseAddress;

    if (accessor.HttpContext != null)
    {
        var request = accessor.HttpContext.Request;
        baseAddress = $"{request.Scheme}://{request.Host}";
    }
    else
    {
        baseAddress = builder.Configuration["Jwt:Issuer"] 
                        ?? throw new InvalidOperationException("HttpContext is null and Jwt:Issuer is not configured.");
    }

    return new HttpClient { BaseAddress = new Uri(baseAddress) };
});

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/forbidden";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
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
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllers();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
    
app.Run();