using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.UnitOfWorks
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepo { get; }
        ITokenRepository TokenRepo { get; }
        IVehicleTypeRepository VehicleTypeRepo { get; }
        IFloorRepository FloorRepo { get; }
        IParkingSessionRepository ParkingSessionRepo { get; }
        IParkingCardRepository ParkingCardRepo { get; }
        IParkingSlotRepository ParkingSlotRepo { get; }
        IMonthlySubscriptionRepository MonthlySubscriptionRepo { get; }
        IGateRepository GateRepo { get; }
        IPricingPolicyRepository PricingPolicyRepo { get; }
        IReservationRepository ReservationRepo { get; }
        IPaymentRepository PaymentRepo { get; }

        Task<int> SaveAsync();  
        Task<bool> SaveChangeAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
