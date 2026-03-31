using DemoSemanticKernel.Models;
using DemoSemanticKernel.Services;
using Microsoft.AspNetCore.Mvc;

namespace DemoSemanticKernel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IConnectionManager _connectionManager;

    public DatabaseController(IDatabaseService databaseService, IConnectionManager connectionManager)
    {
        _databaseService = databaseService;
        _connectionManager = connectionManager;
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectionRequest request)
    {
        var isValid = await _databaseService.TestConnection(request.ConnectionString);
        if (!isValid)
        {
            return BadRequest(new { message = "Invalid connection string or database unreachable" });
        }

        _connectionManager.SetConnection(request.ConnectionString);
        return Ok(new { message = "Connected successfully" });
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables([FromQuery] string? connectionString = null)
    {
        var connStr = connectionString ?? _connectionManager.GetCurrentConnection();
        if (string.IsNullOrEmpty(connStr))
        {
            return BadRequest(new { message = "No database connection established" });
        }

        try
        {
            var tables = await _databaseService.GetTables(connStr);
            return Ok(tables);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving tables: {ex.Message}" });
        }
    }

    [HttpGet("table-names")]
    public async Task<IActionResult> GetTableNames([FromQuery] string? connectionString = null)
    {
        var connStr = connectionString ?? _connectionManager.GetCurrentConnection();
        if (string.IsNullOrEmpty(connStr))
        {
            return BadRequest(new { message = "No database connection established" });
        }

        try
        {
            var tableNames = await _databaseService.GetTableNames(connStr);
            return Ok(tableNames);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving table names: {ex.Message}" });
        }
    }

    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection([FromQuery] string connectionString)
    {
        var isValid = await _databaseService.TestConnection(connectionString);
        return Ok(new { isValid });
    }
}
