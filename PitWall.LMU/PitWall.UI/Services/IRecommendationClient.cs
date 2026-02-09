using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface IRecommendationClient
    {
        Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken);
    }
}
