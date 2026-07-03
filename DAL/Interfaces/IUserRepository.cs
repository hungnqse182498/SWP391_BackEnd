using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> FindByEmailAsync(string email);
        Task<List<User>> GetAllWithRoleAsync();
        Task<User?> GetByIdWithRoleAsync(Guid id);
        Task<User?> FindByPhoneNumberAsync(string phoneNumber);
        Task<bool> IsUserNameDuplicateAsync(string userName, Guid? currentUserId = null);
        Task<bool> IsEmailDuplicateAsync(string email, Guid? currentUserId = null);
        Task<bool> IsPhoneNumberDuplicateAsync(string phoneNumber, Guid? currentUserId = null);
    }
}
