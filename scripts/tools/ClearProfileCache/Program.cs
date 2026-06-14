using Npgsql;

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run -- <connectionString>");
    return 1;
}

await using var conn = new NpgsqlConnection(args[0]);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("""TRUNCATE TABLE "AnalyzedProfiles";""", conn);
await cmd.ExecuteNonQueryAsync();
Console.WriteLine("AnalyzedProfiles: all rows deleted.");
return 0;
