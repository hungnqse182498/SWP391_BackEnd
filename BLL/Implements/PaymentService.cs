using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Payment;
using Common.Enums;
using DAL.UnitOfWorks;

namespace BLL.Implements;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ResponseDTO> PayOSWebhookAsync(PayOSWebhookDTO dto)
    {
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var payment = await _unitOfWork.PaymentRepo.FirstOrDefaultAsync(x => x.TransactionReference == dto.Data.OrderCode.ToString());

            if (payment == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Không tìm thấy thanh toán", 404);
            }

            if (payment.PaymentStatus ==
                PaymentStatus.Success.ToString())
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO("Thanh toán đã được xử lý", 400);
            }

            payment.PaymentStatus = PaymentStatus.Success.ToString();
            payment.PaymentTime = DateTime.UtcNow;

            await _unitOfWork.PaymentRepo.UpdateAsync(payment);

            if (payment.ReservationId.HasValue)
            {
                var reservation = await _unitOfWork.ReservationRepo.GetByIdAsync(payment.ReservationId.Value);
                if (reservation != null)
                {
                    reservation.Status = ReservationStatus.Confirmed.ToString();
                    await _unitOfWork.ReservationRepo.UpdateAsync(reservation);
                }
            }

            await _unitOfWork.SaveAsync();
            await _unitOfWork.CommitTransactionAsync();

            return new ResponseDTO("Thanh toán thành công", 200, true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ResponseDTO(ex.Message, 500);
        }
    }
}