using System.Threading;
using System.Threading.Tasks;

namespace PitWall.UI.Services
{
    public interface ISessionClient
    {
        Task<int> GetSessionCountAsync(CancellationToken cancellationToken);
    }
}
