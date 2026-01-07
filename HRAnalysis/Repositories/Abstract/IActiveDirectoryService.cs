namespace HRAnalysis.Repositories.Abstract
{
    public interface IActiveDirectoryService
    {
        bool IsAuthenticated(string domain, string username, string password, out string errorMessage);
    }
}