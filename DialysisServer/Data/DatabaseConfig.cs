using Microsoft.EntityFrameworkCore;

namespace DialysisServer.Data;

public static class DatabaseConfig
{
    // Centralized, public method to register DB services from configuration.
    public static void ConfigureDatabaseServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Get the provider selector
        var provider = configuration.GetValue<string>("Database:Provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("Configuration error: 'Database:Provider' must be specified and non-empty.");
        }

        // Check existing providers and find the matching one
        var providersSection = configuration.GetSection("Providers");
        var providerSection = providersSection.GetSection(provider);
        if (!providerSection.Exists())
        {
            var available = string.Join(", ", providersSection.GetChildren().Select(c => c.Key));
            throw new InvalidOperationException($"Configuration error: provider '{provider}' not found under 'Providers'. Available providers: {available}");
        }

        // Register the appropriate DbContext based on the provider
        // i.e In normal production Sqlite is used
        // In testing the InMemory is used to not mess with the real database
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var inMemoryName = providerSection.GetValue<string>("Name");
            if (string.IsNullOrWhiteSpace(inMemoryName))
            {
                throw new InvalidOperationException("Configuration error: 'Providers:InMemory:Name' must be specified and non-empty when using the InMemory provider.");
            }

            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(inMemoryName);
                if (environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                }
            });
        }
        else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = providerSection.GetValue<string>("ConnectionString");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Configuration error: 'Providers:Sqlite:ConnectionString' must be specified and non-empty when using the Sqlite provider.");
            }

            services.AddSingleton<DisableWalInterceptor>();
            services.AddDbContextFactory<AppDbContext>((serviceProvider, options) =>
            {
                options.UseSqlite(connectionString, b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name));
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.AddInterceptors(serviceProvider.GetRequiredService<DisableWalInterceptor>());
                if (environment.IsDevelopment())
                {
                    options.EnableSensitiveDataLogging();
                    options.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name });
                }
            });
        }
        else
        {
            throw new InvalidOperationException($"Configuration error: provider '{provider}' is not supported.");
        }
    }
}
