using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.AzureQueue;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using ProjectCallisto.EfCore.Microsoft;
using ProjectCallisto.Resend;
using Resend;
using EmailMessage = ProjectCallisto.Application.Emails.EmailMessage;

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

        // Register DbContext - each scope gets its own instance
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
        
        services.Configure<ResendOptions>(context.Configuration.GetSection("Resend"));
        var resendApiKey = context.Configuration["Resend:ApiKey"];
        services.AddSingleton<IResend>(sp => ResendClient.Create(resendApiKey));

        services.AddScoped<IEmailService, ResendEmailService>();
        
        services.Configure<AzureQueueOptions>(context.Configuration.GetSection("AzureQueue"));
        services.AddScoped<IQueueService<EmailMessage>, AzureQueueService<EmailMessage>>();
        services.AddScoped<IQueueService<ReportCalculationJob>, AzureQueueService<ReportCalculationJob>>();

        // Report calculation services
        services.AddScoped<IReportCalculationService, ReportCalculationService>();
        services.AddScoped<IPresenceBreakdownCalculator, PresenceBreakdownCalculator>();
        services.AddScoped<IInsightDetectionService, InsightDetectionService>();
    })
    .Build();

host.Run();
    