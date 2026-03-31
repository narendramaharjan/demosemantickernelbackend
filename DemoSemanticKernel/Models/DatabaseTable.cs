namespace DemoSemanticKernel.Models;

public class DatabaseTable
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public List<TableColumn> Columns { get; set; } = new List<TableColumn>();
}

public class TableColumn
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}
