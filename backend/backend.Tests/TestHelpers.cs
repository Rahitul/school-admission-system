using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace SchoolAdmission.Tests;

public static class TestConfig
{
    public static string ConnectionString =
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? "Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=postgres";
}

public class TestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConfig.ConnectionString,
            });
        });
        builder.UseEnvironment("Development");
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS") ?? "5");
        Environment.SetEnvironmentVariable("DATABASE_URL", TestConfig.ConnectionString);
    }
}

public static class HttpClientExtensions
{
    public static void SetStudent(this HttpClient c, string id)
    {
        c.DefaultRequestHeaders.Remove("X-Student-Id");
        c.DefaultRequestHeaders.Add("X-Student-Id", id);
    }
}