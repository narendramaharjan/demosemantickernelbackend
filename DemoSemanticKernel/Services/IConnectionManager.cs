namespace DemoSemanticKernel.Services
{
    public interface IConnectionManager
    {
        string? GetCurrentConnection();
        void SetConnection(string connectionString);
        bool IsConnected();
    }
}
