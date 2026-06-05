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
    public class ParkingCardRepository : GenericRepository<ParkingCard>, IParkingCardRepository
    { 
        public ParkingCardRepository(ParkingDBContext context) : base(context) { }

        public async Task<ParkingCard?> GetActiveCardAsync(string cardCode)
        {
            return await _context.ParkingCards
                .FirstOrDefaultAsync(c => c.CardCode == cardCode && c.Status == "Active");
        }
    }
}
