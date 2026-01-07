using HRAnalysis.Repositories.Abstract;

namespace HRAnalysis.Repositories.Implementation
{
    public class StubActiveDirectoryService : IActiveDirectoryService
    {
        private readonly ILogger<StubActiveDirectoryService> _logger;

        public StubActiveDirectoryService(ILogger<StubActiveDirectoryService> logger)
        {
            _logger = logger;
        }

        public bool IsAuthenticated(string domain, string username, string password, out string errorMessage)
        {
            _logger.LogWarning("Active Directory authentication attempted but not supported on this platform. User: {Username}", username);
            errorMessage = "Active Directory authentication is not available on this platform. Please use database authentication.";
            return false;
        }
    }
}