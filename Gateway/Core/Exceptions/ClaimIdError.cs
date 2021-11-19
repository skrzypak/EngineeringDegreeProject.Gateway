using Ocelot.Errors;

namespace Gateway.Core.Errors
{
    public class ClaimIdError : Error
    {
        public ClaimIdError()
            : base("claim_id NOT FOUND", 
                  OcelotErrorCode.CannotFindClaimError,
                  (int) System.Net.HttpStatusCode.BadRequest) {}
    }
}
