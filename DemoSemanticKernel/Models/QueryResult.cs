namespace DemoSemanticKernel.Models
{
    public class QueryResult
    {
        public string GeneratedSql { get; set; } = string.Empty;
        public object? Data { get; set; }
        public List<Dictionary<string, object>>? Rows { get; set; }
        public List<string>? Columns { get; set; }
        public string? Error { get; set; }
        public bool Success { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int RowCount { get; set; }
    }
}
