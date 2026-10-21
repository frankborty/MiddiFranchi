using MiddiFranchi.Application.Financial;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMiddiFranchiInfrastructure();

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<OperationRegistrationService>();
builder.Services.AddScoped<ReversalService>();
builder.Services.AddScoped<OperationHistoryService>();
builder.Services.AddScoped<MovementService>();
builder.Services.AddScoped<ManualBalanceService>();
builder.Services.AddScoped<AccountOverviewService>();
builder.Services.AddScoped<NetWorthOverviewService>();
builder.Services.AddScoped<FinancialDashboardService>();
builder.Services.AddScoped<InvestmentOverviewService>();
builder.Services.AddScoped<DashboardOverviewService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
