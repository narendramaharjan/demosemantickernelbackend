using DemoSemanticKernel.Models;
using System.Data;
using System.Data.SqlClient;

namespace DemoSemanticKernel.Services;

public class DatabaseService : IDatabaseService
{
    public async Task<bool> TestConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<DatabaseTable>> GetTables(string connectionString)
    {
        var tables = new List<DatabaseTable>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Get tables
        var tablesCommand = new SqlCommand(@"
                SELECT 
                    TABLE_SCHEMA,
                    TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME", connection);

        using var tablesReader = await tablesCommand.ExecuteReaderAsync();
        while (await tablesReader.ReadAsync())
        {
            var schema = tablesReader.GetString(0);
            var tableName = tablesReader.GetString(1);

            tables.Add(new DatabaseTable
            {
                Schema = schema,
                TableName = tableName,
                Columns = new List<TableColumn>()
            });
        }
        tablesReader.Close();

        // Get columns for each table
        foreach (var table in tables)
        {
            var columnsCommand = new SqlCommand(@"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.IS_NULLABLE,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                        CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_FOREIGN_KEY
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                        AND c.TABLE_NAME = pk.TABLE_NAME 
                        AND c.COLUMN_NAME = pk.COLUMN_NAME
                    LEFT JOIN (
                        SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                    ) fk ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA 
                        AND c.TABLE_NAME = fk.TABLE_NAME 
                        AND c.COLUMN_NAME = fk.COLUMN_NAME
                    WHERE c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @TableName
                    ORDER BY c.ORDINAL_POSITION", connection);

            columnsCommand.Parameters.AddWithValue("@Schema", table.Schema);
            columnsCommand.Parameters.AddWithValue("@TableName", table.TableName);

            using var columnsReader = await columnsCommand.ExecuteReaderAsync();
            while (await columnsReader.ReadAsync())
            {
                table.Columns.Add(new TableColumn
                {
                    ColumnName = columnsReader.GetString(0),
                    DataType = columnsReader.GetString(1),
                    IsNullable = columnsReader.GetString(2) == "YES",
                    IsPrimaryKey = columnsReader.GetInt32(3) == 1,
                    IsForeignKey = columnsReader.GetInt32(4) == 1
                });
            }
            columnsReader.Close();
        }

        return tables;
    }

    public async Task<List<Dictionary<string, object>>> ExecuteQuery(string connectionString, string sqlQuery)
    {
        var results = new List<Dictionary<string, object>>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(sqlQuery, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnName] = value!;
            }
            results.Add(row);
        }

        return results;
    }

    public async Task<List<string>> GetTableNames(string connectionString)
    {
        var tableNames = new List<string>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
                SELECT CONCAT(TABLE_SCHEMA, '.', TABLE_NAME) AS FullTableName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME", connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    public async Task<DatabaseTable> GetTableSchema(string connectionString, string tableName)
    {
        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var actualTableName = parts.Length > 1 ? parts[1] : parts[0];

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var table = new DatabaseTable
        {
            Schema = schema,
            TableName = actualTableName,
            Columns = new List<TableColumn>()
        };

        var command = new SqlCommand(@"
                SELECT 
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION", connection);

        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@TableName", actualTableName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            table.Columns.Add(new TableColumn
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES"
            });
        }

        return table;
    }
}
