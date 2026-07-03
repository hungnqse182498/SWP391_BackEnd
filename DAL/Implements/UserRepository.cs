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
        public UserRepository(ParkingDBContext context) : base(context)
        {
        }
        public async Task<User?> FindByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var normalizedEmail = email.Trim().ToLower();
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        }

        public async Task<List<User>> GetAllWithRoleAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .OrderBy(u => u.UserName)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<User?> GetByIdWithRoleAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.Role) 
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        public async Task<User?> FindByPhoneNumberAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

            var normalizedPhoneNumber = phoneNumber.Trim();
            return await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber);
        }

        public async Task<bool> IsUserNameDuplicateAsync(string userName, Guid? currentUserId = null)
        {
            var normalizedUserName = userName.Trim().ToLower();
            return await _context.Users.AnyAsync(u =>
                u.UserName.ToLower() == normalizedUserName &&
                (!currentUserId.HasValue || u.UserId != currentUserId.Value));
        }

        public async Task<bool> IsEmailDuplicateAsync(string email, Guid? currentUserId = null)
        {
            var normalizedEmail = email.Trim().ToLower();
            return await _context.Users.AnyAsync(u =>
                u.Email.ToLower() == normalizedEmail &&
                (!currentUserId.HasValue || u.UserId != currentUserId.Value));
        }

        public async Task<bool> IsPhoneNumberDuplicateAsync(string phoneNumber, Guid? currentUserId = null)
        {
            var normalizedPhoneNumber = phoneNumber.Trim();
            return await _context.Users.AnyAsync(u =>
                u.PhoneNumber == normalizedPhoneNumber &&
                (!currentUserId.HasValue || u.UserId != currentUserId.Value));
        }
    }
}
