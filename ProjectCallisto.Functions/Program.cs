using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using ProjectCallisto.EfCore.Microsoft;

// var builder = FunctionsApplication.CreateBuilder(args);
//
// builder.ConfigureFunctionsWebApplication();
//
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();
//
// builder.Build().Run();

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
            options.UseSqlServer(connectionString);
        });
        
        services.AddHttpClient<IMicrosoftGraphService, MicrosoftGraphService>();
        services.AddHttpClient<IMicrosoftTokenService, MicrosoftTokenService>();
        
        services.Configure<MicrosoftGraphOptions>(
            context.Configuration.GetSection("AzureAd"));
        
        services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
        services.AddScoped<IMicrosoftTokenService, MicrosoftTokenService>();
        services.AddScoped<IMicrosoftConnectionRepository, MicrosoftConnectionRepository>();
    })
    .Build();

host.Run();
    