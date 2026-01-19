using System.Text.Json;
using System.Text.Json.Serialization;
using ArtNet_Dmx_Lights.Api;
using ArtNet_Dmx_Lights.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddSingleton(new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
});
builder.Services.AddSingleton<IAppStateStore>(sp =>
    new AppStateStore(sp.GetRequiredService<JsonSerializerOptions>(), builder.Environment.ContentRootPath));
builder.Services.AddSingleton<ValidationService>();
builder.Services.AddHttpClient<IGeoTimeLookupService, GeoTimeLookupService>();
builder.Services.AddHttpClient<SunriseSunsetService>();
builder.Services.AddSingleton<DmxStateCache>();
builder.Services.AddSingleton<IArtNetSender, ArtNetSender>();
builder.Services.AddSingleton<DmxEngine>();
builder.Services.AddSingleton<ArtNetDiscoveryService>();
builder.Services.AddSingleton<ScheduleEvaluator>();
builder.Services.AddSingleton<LogRetentionPolicy>();
builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddHostedService<LogRetentionService>();
builder.Services.AddHostedService<StartupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapSettingsEndpoints();
app.MapGroupEndpoints();
app.MapPresetEndpoints();
app.MapScheduleEndpoints();
app.MapLogEndpoints();
app.MapBackupEndpoints();
app.MapStatusEndpoints();
app.MapArtNetEndpoints();
app.MapRazorPages()
   .WithStaticAssets();

await app.Services.GetRequiredService<IAppStateStore>().InitializeAsync(CancellationToken.None);

app.Run();

public partial class Program
{
}
