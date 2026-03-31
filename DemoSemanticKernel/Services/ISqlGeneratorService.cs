using DemoSemanticKernel.Models;

namespace DemoSemanticKernel.Services;

public interface ISqlGeneratorService
{
    Task<string> GenerateSqlFromText(string naturalLanguageQuery, List<DatabaseTable> tables);
    Task<QueryResult> ExecuteNaturalLanguageQuery(QueryRequest request, string connectionString);
}
