using System;
using System.Collections.Generic;

namespace PitWall.Agent.Services.LLM
{
    public interface ILlmEndpointEnumerator
    {
        IEnumerable<Uri> GetCandidateEndpoints();
    }
}
