using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Endpoints.Market;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Extensions.DI;
using Fc25Draft.Web.Extensions.Endpoints;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Security;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfra(builder.Configuration, builder.Environment)
    .AddCoreServices(builder.Configuration)
    .AddMarketHistoryFeature();

builder.Services
    .AddAuthentication(opts => {
        opts.DefaultAuthenticateScheme = AdminTokenAuthenticationHandler.SchemeName;
        opts.DefaultChallengeScheme = AdminTokenAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
        AdminTokenAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")));
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(o => o.DetailedErrors = true);
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.MigrateAsync();
    }
}
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.SeedDatabaseAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DraftHub>("/hubs/draft");
app.MapHub<MarketHub>("/hubs/market");

var api = app.MapGroup("/api");
api.MapDraftEndpoints()
   .MapPlayerEndpoints()
   .MapTeamEndpoints()
   .MapBudgetEndpoints()
   .MapPricingEndpoints()
   .MapTransfersEndpoints()
   .MapMarketEndpoints()
   .MapMarketHistoryEndpoints()
   .MapAdminEndpoints();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapHealthChecks("/health");

app.Run();
