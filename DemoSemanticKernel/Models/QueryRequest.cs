namespace DemoSemanticKernel.Models
{
    public class QueryRequest
    {
        public string NaturalLanguageQuery { get; set; } = string.Empty;
        public List<string> SelectedTables { get; set; } = new List<string>();
        public string? ConnectionString { get; set; }
    }
}
