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
    public class RoleRepository : GenericRepository<Role>, IRoleRepository
    {
        public RoleRepository(ParkingDBContext context) : base(context)
        {
        }
        public async Task<List<Role>> GetAllOrderedByNameAsync()
        {
            return await _context.Roles
                .OrderBy(r => r.RoleName)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Role>> GetAssignableRolesAsync(string excludedRoleName)
        {
            var normalizedExcludedRoleName = excludedRoleName.Trim().ToLower();

            return await _context.Roles
                .Where(r => r.RoleName.ToLower() != normalizedExcludedRoleName)
                .OrderBy(r => r.RoleName)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Role?> GetRoleByNameAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return null;

            var normalizedRoleName = roleName.Trim().ToLower();
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName.ToLower() == normalizedRoleName);
        }

        public async Task<bool> IsRoleNameDuplicateAsync(string roleName, Guid? currentRoleId = null)
        {
            var normalizedRoleName = roleName.Trim().ToLower();

            return await _context.Roles.AnyAsync(r =>
                r.RoleName.ToLower() == normalizedRoleName &&
                (!currentRoleId.HasValue || r.RoleId != currentRoleId.Value));
        }

        public async Task<bool> HasUsersAsync(Guid roleId)
        {
            return await _context.Users.AnyAsync(u => u.RoleId == roleId);
        }
    }
}
