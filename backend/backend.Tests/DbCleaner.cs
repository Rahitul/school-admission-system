using Npgsql;

namespace SchoolAdmission.Tests;

public static class DbCleaner
{
    public static async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(TestConfig.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE TABLE holds, classes RESTART IDENTITY CASCADE;", conn);
        try { await cmd.ExecuteNonQueryAsync(); } catch { /* tables may not exist yet */ }
    }
}