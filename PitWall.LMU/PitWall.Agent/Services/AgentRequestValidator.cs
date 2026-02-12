using System.Collections.Generic;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services
{
    public static class AgentRequestValidator
    {
        public static IReadOnlyList<string> ValidateQuery(AgentRequest? request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("Request body is required.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                errors.Add("Query is required.");
            }

            return errors;
        }
    }
}
