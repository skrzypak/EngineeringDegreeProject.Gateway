using Ocelot.Errors;

namespace Gateway.Core.Errors
{
    public class AuthoriseEspError : Error
    {
        public AuthoriseEspError(string espId)
            : base($"NO PERMISSIONS to enterprise with ID: {espId}", 
                  OcelotErrorCode.ScopeNotAuthorizedError,
                  (int) System.Net.HttpStatusCode.Forbidden) {}
    }
}
