using System.Text;
using System.Text.Json;
using DialysisServer.Data;
using DialysisServer.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DialysisServer.Tests;

static class TestHost
{
    public static WebApplicationFactory<Program> CreateFactory(string? inMemoryDbName = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, cfg) =>
                {
                    var col = new List<KeyValuePair<string, string?>>();
                    col.Add(new KeyValuePair<string, string?>("Database:Provider", "InMemory"));
                    if (inMemoryDbName != null) col.Add(new KeyValuePair<string, string?>("Providers:InMemory:Name", inMemoryDbName));

                    cfg.AddInMemoryCollection(col);
                });
            });

    public static IDbContextFactory<AppDbContext> GetDbFactory(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
}

public class DatabaseTests
{
    private static readonly JsonSerializerOptions ClientJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetDefaultEndpoint_PersistsOneRecord()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var factory = TestHost.CreateFactory(dbName);
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/test/default");
        res.EnsureSuccessStatusCode();

        var dbFactory = TestHost.GetDbFactory(factory);
        await using var ctx = dbFactory.CreateDbContext();
        var single = await ctx.SensorData.SingleAsync();
        single.Cadence.Should().Be(90);
        single.Speed.Should().BeApproximately(30.2, 0.0001);
    }

    [Fact]
    public async Task PostAddEndpoint_PersistsPostedData()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var factory = TestHost.CreateFactory(dbName);
        var client = factory.CreateClient();

        var payload = new { cadence = 33, speed = 80.2 };
        var json = JsonSerializer.Serialize(payload, ClientJsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var res = await client.PostAsync("/api/test/add", content);
        res.EnsureSuccessStatusCode();

        var dbFactory = TestHost.GetDbFactory(factory);
        await using var ctx = dbFactory.CreateDbContext();
        var single = await ctx.SensorData.SingleAsync();
        single.Cadence.Should().Be(33);
        single.Speed.Should().BeApproximately(80.2, 0.0001);
    }

    [Fact]
    public async Task GetAllEndpoint_ReturnsSeededItems()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var factory = TestHost.CreateFactory(dbName);
        var dbFactory = TestHost.GetDbFactory(factory);

        // seed two entries
        await using (var ctx = dbFactory.CreateDbContext())
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.SensorData.AddRange(
                new SensorData { Cadence = 1, Speed = 1.0, Timestamp = DateTime.UtcNow },
                new SensorData { Cadence = 2, Speed = 2.0, Timestamp = DateTime.UtcNow }
            );
            await ctx.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/test/all");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }
}
