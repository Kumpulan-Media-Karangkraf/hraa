using HRAnalysis.Repositories.Abstract;
using System.DirectoryServices.Protocols;
using System.Net;

namespace HRAnalysis.Repositories.Implementation
{
    public class ActiveDirectoryServiceLdap : IActiveDirectoryService
    {
        private readonly string _server;
        private readonly string _domain;

        public ActiveDirectoryServiceLdap(string server, string domain)
        {
            _server = server; // e.g., "main-srvhrsweb-02"
            _domain = domain;
        }

        public bool IsAuthenticated(string domain, string username, string password, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(username))
            {
                errorMessage = "Please enter your username";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Please enter your password";
                return false;
            }

            try
            {
                using var connection = new LdapConnection(_server);
                connection.Credential = new NetworkCredential($"{_domain}\\{username}", password);
                connection.AuthType = AuthType.Negotiate;

                // Attempt to bind - this will throw if authentication fails
                connection.Bind();

                // Optional: Search for the user to verify they exist
                var searchRequest = new SearchRequest(
                    $"DC={_domain.Replace(".", ",DC=")}",
                    $"(SAMAccountName={username})",
                    SearchScope.Subtree,
                    "cn"
                );

                var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                if (searchResponse.Entries.Count == 0)
                {
                    errorMessage = "User not found in directory";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
