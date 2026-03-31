using DemoSemanticKernel.Models;

namespace DemoSemanticKernel.Services
{
    public interface IDatabaseService
    {
        Task<bool> TestConnection(string connectionString);
        Task<List<DatabaseTable>> GetTables(string connectionString);
        Task<List<Dictionary<string, object>>> ExecuteQuery(string connectionString, string sqlQuery);
        Task<List<string>> GetTableNames(string connectionString);
        Task<DatabaseTable> GetTableSchema(string connectionString, string tableName);
    }
}
