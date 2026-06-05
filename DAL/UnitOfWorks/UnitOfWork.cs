using DAL.Implements;
using DAL.Interfaces;
using DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.UnitOfWorks
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ParkingDBContext _context;


        public UnitOfWork(ParkingDBContext context)
        {
            _context = context;
            UserRepo = new UserRepository(_context);
            TokenRepo = new TokenRepository(_context);
            VehicleTypeRepo = new VehicleTypeRepository(_context);
            FloorRepo = new FloorRepository(_context);
            GateRepo = new GateRepository(_context);
            ParkingSlotRepo = new ParkingSlotRepository(_context);
            ParkingCardRepo = new ParkingCardRepository(_context);
            MonthlySubscriptionRepo = new MonthlySubscriptionRepository(_context);
            PricingPolicyRepo = new PricingPolicyRepository(_context);
            ReservationRepo = new ReservationRepository(_context);
            ParkingSessionRepo = new ParkingSessionRepository(_context);
            PaymentRepo = new PaymentRepository(_context);
            IncidentReportRepo = new IncidentReportRepository(_context);
        }

        public IUserRepository UserRepo { get; private set; }
        public ITokenRepository TokenRepo { get; private set; }
        public IVehicleTypeRepository VehicleTypeRepo { get; private set; }

        public IFloorRepository FloorRepo { get; private set; }
        public IGateRepository GateRepo { get; private set; }
        public IParkingSlotRepository ParkingSlotRepo { get; private set; }
        public IParkingCardRepository ParkingCardRepo { get; private set; }
        public IMonthlySubscriptionRepository MonthlySubscriptionRepo { get; private set; }
        public IPricingPolicyRepository PricingPolicyRepo { get; private set; }
        public IReservationRepository ReservationRepo { get; private set; }
        public IParkingSessionRepository ParkingSessionRepo { get; private set; }
        public IPaymentRepository PaymentRepo { get; private set; }
        public IIncidentReportRepository IncidentReportRepo { get; private set; }

        public void Dispose()
        {
            _context.Dispose();
        }
        public async Task<int> SaveAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> SaveChangeAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
