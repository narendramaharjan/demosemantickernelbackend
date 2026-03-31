using DemoSemanticKernel.Models;
using DemoSemanticKernel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DemoSemanticKernel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly ISqlGeneratorService _sqlGeneratorService;
    private readonly IConnectionManager _connectionManager;
    private readonly IDatabaseService _databaseService;

    public QueryController(
        ISqlGeneratorService sqlGeneratorService,
        IConnectionManager connectionManager,
        IDatabaseService databaseService)
    {
        _sqlGeneratorService = sqlGeneratorService;
        _connectionManager = connectionManager;
        _databaseService = databaseService;
    }

    [HttpPost("natural-language")]
    public async Task<IActionResult> ExecuteNaturalLanguageQuery([FromBody] QueryRequest request)
    {
        var connectionString = request.ConnectionString ?? _connectionManager.GetCurrentConnection();
        if (string.IsNullOrEmpty(connectionString))
        {
            return BadRequest(new { message = "No database connection established" });
        }

        if (string.IsNullOrEmpty(request.NaturalLanguageQuery))
        {
            return BadRequest(new { message = "Query text is required" });
        }

        if (!request.SelectedTables.Any())
        {
            return BadRequest(new { message = "At least one table must be selected" });
        }

        try
        {
            var result = await _sqlGeneratorService.ExecuteNaturalLanguageQuery(request, connectionString);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error executing query",
                error = ex.Message,
                success = false
            });
        }
    }

    [HttpPost("execute-sql")]
    public async Task<IActionResult> ExecuteSql([FromBody] ExecuteSqlRequest request)
    {
        var connectionString = request.ConnectionString ?? _connectionManager.GetCurrentConnection();
        if (string.IsNullOrEmpty(connectionString))
        {
            return BadRequest(new { message = "No database connection established" });
        }

        if (string.IsNullOrEmpty(request.SqlQuery))
        {
            return BadRequest(new { message = "SQL query is required" });
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var data = await _databaseService.ExecuteQuery(connectionString, request.SqlQuery);
            stopwatch.Stop();

            return Ok(new QueryResult
            {
                GeneratedSql = request.SqlQuery,
                Data = data,
                Rows = data,
                Columns = data.Any() ? data[0].Keys.ToList() : new List<string>(),
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                RowCount = data.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error executing SQL query",
                error = ex.Message,
                success = false
            });
        }
    }
}

public class ExecuteSqlRequest
{
    public string SqlQuery { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
}
