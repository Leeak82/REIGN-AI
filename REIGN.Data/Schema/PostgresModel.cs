using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace REIGN.Data.Schema;

/// <summary>
/// Supabase's postgres database already exists. EF EnsureCreated no-ops when any
/// public table is present, so Businesses never gets created. Create missing tables
/// from the current model.
/// </summary>
public static class PostgresModel
{
    public static async Task EnsureCreatedAsync(ReignDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await TableExistsAsync(db, "Businesses", cancellationToken))
        {
            return;
        }

        var creator = db.GetService<IRelationalDatabaseCreator>();
        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsAlreadyExists(ex))
        {
            await ApplyMissingObjectsAsync(db, cancellationToken);
        }

        if (!await TableExistsAsync(db, "Businesses", cancellationToken))
        {
            await ApplyMissingObjectsAsync(db, cancellationToken);
        }
    }

    public static bool IsMissingRelation(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == "42P01")
            {
                return true;
            }

            if (current.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                && current.Message.Contains("Businesses", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> SplitCreateScript(string script)
    {
        var batches = new List<string>();
        foreach (var raw in script.Split([";\r\n", ";\n", ";\r"], StringSplitOptions.None))
        {
            var sql = raw.Trim();
            if (sql.Length == 0 || sql.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            batches.Add(sql);
        }

        return batches;
    }

    public static async Task<bool> TableExistsAsync(
        ReignDbContext db,
        string table,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = ANY (current_schemas(false))
                  AND lower(table_name) = lower(@name)
                LIMIT 1
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = table;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && result != DBNull.Value;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ApplyMissingObjectsAsync(ReignDbContext db, CancellationToken cancellationToken)
    {
        foreach (var sql in SplitCreateScript(db.Database.GenerateCreateScript()))
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            catch (Exception ex) when (IsAlreadyExists(ex))
            {
            }
        }
    }

    private static bool IsAlreadyExists(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState is "42P07" or "42710" or "23505")
            {
                return true;
            }
        }

        return false;
    }
}
