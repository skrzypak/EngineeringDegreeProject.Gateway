using Ocelot.Errors;

namespace Gateway.Core.Errors
{
    public class OcelotKeyError : Error
    {
        public OcelotKeyError()
            : base("Null KEY - contact with support", 
                  OcelotErrorCode.UnknownError,
                  (int) System.Net.HttpStatusCode.InternalServerError) {}
    }
}
