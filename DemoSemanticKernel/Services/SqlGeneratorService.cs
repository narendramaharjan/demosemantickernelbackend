using DemoSemanticKernel.Models;
using DemoSemanticKernel.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using System.Text.Json;
using DemoSemanticKernel.Models;

namespace DemoSemanticKernel.Services
{
    public class SqlGeneratorService : ISqlGeneratorService
    {
        private readonly IConfiguration _configuration;
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<SqlGeneratorService> _logger;

        public SqlGeneratorService(IConfiguration configuration,
                                  IDatabaseService databaseService,
                                  ILogger<SqlGeneratorService> logger)
        {
            _configuration = configuration;
            _databaseService = databaseService;
            _logger = logger;
        }

        public async Task<string> GenerateSqlFromText(string naturalLanguageQuery, List<DatabaseTable> tables)
        {
            try
            {
                var apiKey = _configuration["OpenRouter:ApiKey"];
                var model = _configuration["OpenRouter:Model"] ?? "deepseek/deepseek-chat";
                var baseUrl = _configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";

                _logger.LogInformation($"Using OpenRouter API. Model: {model}, BaseUrl: {baseUrl}");

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("OpenRouter API key is not configured. Please add it to appsettings.json or environment variables.");
                }

                // Build schema context
                var schemaContext = BuildSchemaContext(tables);

                var prompt = $"""
                    You are an expert SQL developer specializing in Microsoft SQL Server (T-SQL). 
                    Generate a SQL Server compatible SQL query based on the user's request.
                    Only use the tables and columns provided in the schema. Do not make up tables or columns.

                    Database Schema:
                    {schemaContext}

                    User Request: {naturalLanguageQuery}

                    Rules:
                    1. Generate ONLY the SQL query, no explanations, no markdown, no code blocks
                    2. Use proper SQL Server syntax (T-SQL)
                    3. Use meaningful aliases when needed
                    4. Include proper JOINs based on foreign key relationships
                    5. Handle NULL values appropriately
                    6. Use SELECT TOP 100 if no specific limit is requested
                    7. Format the SQL query for readability with proper indentation
                    8. If the query asks for aggregation, include GROUP BY
                    9. If filtering is needed, use WHERE clause
                    10. ORDER BY is often helpful for readability

                    Important: Return ONLY the SQL query, nothing else. No additional text.

                    SQL Query:
                    """;

                _logger.LogDebug($"Prompt generated. Length: {prompt.Length}");

                // Create HTTP client with proper headers for OpenRouter
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:3000");
                httpClient.DefaultRequestHeaders.Add("X-Title", "Text to SQL Converter");

                // Prepare the request
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = 0.1,
                    max_tokens = 1000
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Sending request to OpenRouter. Model: {model}");

                var response = await httpClient.PostAsync($"{baseUrl}/chat/completions", content);

                _logger.LogInformation($"OpenRouter response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"OpenRouter API error: {errorContent}");
                    throw new Exception($"OpenRouter API returned status {response.StatusCode}: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Raw OpenRouter response: {responseContent}");

                // Parse the JSON response
                using var jsonDoc = JsonDocument.Parse(responseContent);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.GetProperty("message").GetString();
                    throw new Exception($"OpenRouter API error: {errorMessage}");
                }

                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                {
                    throw new Exception("No response from OpenRouter API");
                }

                var firstChoice = choices[0];
                var message = firstChoice.GetProperty("message");
                var sql = message.GetProperty("content").GetString()?.Trim();

                if (string.IsNullOrEmpty(sql))
                {
                    throw new Exception("Empty response from OpenRouter API");
                }

                // Clean up the result
                sql = CleanSqlResponse(sql);

                _logger.LogInformation($"Generated SQL: {sql}");
                return sql;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating SQL: {ex.Message}");

                // Fallback: Return a simple query if AI fails
                return GenerateFallbackSql(naturalLanguageQuery, tables);
            }
        }

        private string CleanSqlResponse(string sql)
        {
            // Remove markdown code blocks
            if (sql.StartsWith("```sql"))
            {
                sql = sql.Substring(6);
            }
            if (sql.EndsWith("```"))
            {
                sql = sql.Substring(0, sql.Length - 3);
            }

            // Remove any leading/trailing quotes
            sql = sql.Trim('"', '\'', '`');

            // Remove common prefixes
            var prefixes = new[] { "SELECT", "WITH", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP" };
            var lines = sql.Split('\n');

            // Find the first line that starts with a SQL keyword
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                foreach (var prefix in prefixes)
                {
                    if (trimmedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return sql.Substring(sql.IndexOf(line));
                    }
                }
            }

            return sql.Trim();
        }

        private string GenerateFallbackSql(string naturalLanguageQuery, List<DatabaseTable> tables)
        {
            // Simple fallback logic
            var table = tables.FirstOrDefault();
            if (table == null)
            {
                return "SELECT 1 AS Fallback";
            }

            var columns = string.Join(", ", table.Columns.Take(5).Select(c => $"[{c.ColumnName}]"));

            return $"SELECT TOP 100 {columns} FROM [{table.Schema}].[{table.TableName}]";
        }

        private string BuildSchemaContext(List<DatabaseTable> tables)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available Tables:");
            sb.AppendLine("=================");

            foreach (var table in tables)
            {
                sb.AppendLine($"Table: [{table.Schema}].[{table.TableName}]");
                foreach (var column in table.Columns)
                {
                    var flags = new List<string>();
                    if (column.IsPrimaryKey) flags.Add("PRIMARY KEY");
                    if (column.IsForeignKey) flags.Add("FOREIGN KEY");
                    if (!column.IsNullable) flags.Add("NOT NULL");

                    var flagsStr = flags.Any() ? $" -- {string.Join(", ", flags)}" : "";
                    sb.AppendLine($"  - {column.ColumnName}: {column.DataType}{flagsStr}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public async Task<QueryResult> ExecuteNaturalLanguageQuery(QueryRequest request, string connectionString)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Executing natural language query: {request.NaturalLanguageQuery}");
                _logger.LogInformation($"Selected tables: {string.Join(", ", request.SelectedTables)}");

                // Get table schemas for selected tables
                var tables = new List<DatabaseTable>();
                foreach (var tableName in request.SelectedTables)
                {
                    var table = await _databaseService.GetTableSchema(connectionString, tableName);
                    tables.Add(table);
                    _logger.LogDebug($"Loaded schema for table: {tableName} with {table.Columns.Count} columns");
                }

                // Generate SQL
                var generatedSql = await GenerateSqlFromText(request.NaturalLanguageQuery, tables);

                _logger.LogInformation($"Generated SQL: {generatedSql}");

                // Execute the query
                var data = await _databaseService.ExecuteQuery(connectionString, generatedSql);

                stopwatch.Stop();

                _logger.LogInformation($"Query executed successfully in {stopwatch.ElapsedMilliseconds}ms. Returned {data.Count} rows.");

                return new QueryResult
                {
                    GeneratedSql = generatedSql,
                    Data = data,
                    Rows = data,
                    Columns = data.Any() ? data[0].Keys.ToList() : new List<string>(),
                    Success = true,
                    ExecutionTime = stopwatch.Elapsed,
                    RowCount = data.Count
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error executing natural language query: {ex.Message}");

                return new QueryResult
                {
                    Error = ex.Message,
                    Success = false,
                    ExecutionTime = stopwatch.Elapsed
                };
            }
        }
    }
}