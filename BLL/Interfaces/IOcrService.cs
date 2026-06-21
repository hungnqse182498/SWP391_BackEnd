using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BLL.Interfaces
{
    public interface IOcrService
    {
        Task<string?> RecognizeLicensePlateAsync(
            Stream imageStream,
            string fileName,
            CancellationToken cancellationToken = default);
    }
}
