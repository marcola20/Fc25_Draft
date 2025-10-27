using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;                 
using Fc25Draft.Infra.Repositories;
using Fc25Draft.Web.Extensions;            
using Fc25Draft.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<DraftDbContext>(opt =>
    opt.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName))
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
       .EnableDetailedErrors(builder.Environment.IsDevelopment())
);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<DraftService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPositionService, PositionService>();

var app = builder.Build();

await app.SeedDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapPost("/api/draft/reset", async (DraftService draftService, CancellationToken ct) =>
{
    await draftService.ResetDraftAsync(ct);
    return Results.NoContent();
});

app.Run();
