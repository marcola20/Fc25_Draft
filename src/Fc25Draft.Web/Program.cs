using Fc25Draft.Web.Data;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
       .EnableDetailedErrors(builder.Environment.IsDevelopment())
);

// Blazor
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<DraftService>();

var app = builder.Build();

await app.SeedDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//if (app.Environment.IsDevelopment())
//{
//    using var scope = app.Services.CreateScope();
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    var svc = scope.ServiceProvider.GetRequiredService<DraftService>();

//    var teamOrder = await db.Teams
//        .OrderBy(t => t.TeamName)
//        .Select(t => t.TeamId)
//        .Take(14)
//        .ToListAsync();

//    if (!await db.Drafts.AnyAsync())
//        await svc.CreateDraftAsync("FC25 - Draft de Teste", teamOrder, totalRounds: 19, snake: true);
//}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
