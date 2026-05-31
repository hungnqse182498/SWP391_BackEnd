using DAL.Interfaces;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        private readonly ParkingDBContext _context;

        public UserRepository(ParkingDBContext context) : base(context)
        {
            _context = context;
        }
        public async Task<User> FindByEmailAsync(string email)
        {
            return await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<Guid> GetRoleIdByNameAsync(string roleName)
        {
            var normalizedRoleName = roleName.ToLower();
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName.ToLower() == normalizedRoleName);
            return role != null ? role.RoleId : Guid.Empty;
        }

        public async Task<User> GetByIdWithRoleAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Role) 
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        public async Task<Role> GetRoleByIdAsync(Guid roleId)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
        }

        public async Task<List<Role>> GetManageableRolesAsync()
        {
            var manageableRoleNames = new[] { "driver", "staff", "manager" };

            return await _context.Roles
                .Where(r => manageableRoleNames.Contains(r.RoleName.ToLower()))
                .OrderBy(r => r.RoleName)
                .ToListAsync();
        }
    }
}
