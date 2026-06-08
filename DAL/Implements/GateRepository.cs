using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Implements
{
    public class GateRepository : GenericRepository<Gate>, IGateRepository
    { public GateRepository(ParkingDBContext context) : base(context) { } }

}
