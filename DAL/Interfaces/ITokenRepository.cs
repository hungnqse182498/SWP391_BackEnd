using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface ITokenRepository : IGenericRepository<RefreshToken>
    {
        Task<RefreshToken> GetRefreshTokenByUserID(Guid userId);
        Task<RefreshToken?> GetRefreshTokenByKey(string refreshTokenKey);
    }
}
