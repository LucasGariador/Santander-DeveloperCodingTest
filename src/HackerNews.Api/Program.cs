using Microsoft.Extensions.Options;
using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Services;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración tipada
builder.Services.Configure<HackerNewsOptions>(
    builder.Configuration.GetSection(HackerNewsOptions.SectionName));

// 2. Servicios base de API y Caché
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

// 3. Typed HttpClient + Resiliencia estándar (Retries, Circuit Breaker, Timeouts)
builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddTransientHttpErrorPolicy(policyBuilder => 
    policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// 4. Registro de Servicios de Negocio (Singleton porque gestiona el SemaphoreSlim de concurrencia)
builder.Services.AddSingleton<IStoriesService, StoriesService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Necesario si después querés hacer Integration Tests con WebApplicationFactory
public partial class Program { }