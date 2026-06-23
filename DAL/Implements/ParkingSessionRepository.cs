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
    public class ParkingSessionRepository : GenericRepository<ParkingSession>, IParkingSessionRepository
    { 
        public ParkingSessionRepository(ParkingDBContext context) : base(context) { }

    }
}
