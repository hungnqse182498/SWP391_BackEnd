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
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}
