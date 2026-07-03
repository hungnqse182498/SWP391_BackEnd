using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IRoleRepository : IGenericRepository<Role>
    {
        Task<List<Role>> GetAllOrderedByNameAsync();
        Task<List<Role>> GetAssignableRolesAsync(string excludedRoleName);
        Task<Role?> GetRoleByNameAsync(string roleName);
        Task<bool> IsRoleNameDuplicateAsync(string roleName, Guid? currentRoleId = null);
        Task<bool> HasUsersAsync(Guid roleId);
    }
}
