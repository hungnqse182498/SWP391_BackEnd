using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.ParkingCard;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class ParkingCardService : IParkingCardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active",
            "InUse",
            "Lost",
            "Inactive"
        };

        public ParkingCardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var cards = await _unitOfWork.ParkingCardRepo.GetAll()
                .OrderBy(c => c.CardCode)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách thẻ xe thành công", 200, true, cards.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập CardId", 400, false);

            var card = await _unitOfWork.ParkingCardRepo.GetByIdAsync(id);
            if (card == null) return new ResponseDTO("Không tìm thấy thẻ xe", 404, false);
            return new ResponseDTO("Lấy thông tin thẻ xe thành công", 200, true, MapToDTO(card));
        }

        public async Task<ResponseDTO> CreateAsync(CreateParkingCardDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo thẻ xe không hợp lệ", 400, false);

            var validation = await ValidateCardAsync(dto.CardCode, dto.Status ?? "Active", null);
            if (validation.Error != null) return validation.Error;

            var card = new ParkingCard
            {
                CardId = Guid.NewGuid(),
                CardCode = dto.CardCode.Trim(),
                Status = validation.Status!
            };

            await _unitOfWork.ParkingCardRepo.AddAsync(card);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Tạo thẻ xe thành công", 201, true, MapToDTO(card));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateParkingCardDTO dto)
        {
            if (dto == null || dto.CardId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật thẻ xe không hợp lệ", 400, false);

            var card = await _unitOfWork.ParkingCardRepo.GetByIdAsync(dto.CardId);
            if (card == null) return new ResponseDTO("Không tìm thấy thẻ xe", 404, false);

            var validation = await ValidateCardAsync(dto.CardCode, dto.Status, dto.CardId);
            if (validation.Error != null) return validation.Error;

            card.CardCode = dto.CardCode.Trim();
            card.Status = validation.Status!;

            await _unitOfWork.ParkingCardRepo.UpdateAsync(card);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseDTO("Cập nhật thẻ xe thành công", 200, true, MapToDTO(card));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập CardId", 400, false);

            var card = await _unitOfWork.ParkingCardRepo.GetByIdAsync(id);
            if (card == null) return new ResponseDTO("Không tìm thấy thẻ xe", 404, false);

            try
            {
                _unitOfWork.ParkingCardRepo.Delete(card);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa thẻ xe thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa thẻ xe: {ex.Message}", 500, false);
            }
        }

        private async Task<(string? Status, ResponseDTO? Error)> ValidateCardAsync(string? cardCode, string? status, Guid? currentCardId)
        {
            if (string.IsNullOrWhiteSpace(cardCode)) return (null, new ResponseDTO("Vui lòng nhập mã thẻ xe", 400, false));

            var normalizedStatus = NormalizeStatus(status);
            if (normalizedStatus == null) return (null, new ResponseDTO("Trạng thái thẻ xe chỉ được là Active, InUse, Lost hoặc Inactive", 400, false));

            var code = cardCode.Trim().ToLower();
            var duplicate = await _unitOfWork.ParkingCardRepo.GetAll()
                .AnyAsync(c => c.CardCode.ToLower() == code && (!currentCardId.HasValue || c.CardId != currentCardId.Value));
            if (duplicate) return (null, new ResponseDTO("Mã thẻ xe đã tồn tại", 400, false));

            return (normalizedStatus, null);
        }

        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return ValidStatuses.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static ParkingCardDTO MapToDTO(ParkingCard card)
        {
            return new ParkingCardDTO
            {
                CardId = card.CardId,
                CardCode = card.CardCode,
                Status = card.Status
            };
        }
    }
}
