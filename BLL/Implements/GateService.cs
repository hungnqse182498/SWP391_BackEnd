using BLL.Interfaces;
using Common.DTOs;
using Common.DTOs.Gate;
using DAL.Models;
using DAL.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace BLL.Implements
{
    public class GateService : IGateService
    {
        private readonly IUnitOfWork _unitOfWork;

        public GateService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetAllAsync()
        {
            var gates = await _unitOfWork.GateRepo.GetAll()
                .Include(g => g.Floor)
                .OrderBy(g => g.Floor.FloorName)
                .ThenBy(g => g.GateType)
                .ToListAsync();

            return new ResponseDTO("Lấy danh sách cổng thành công", 200, true, gates.Select(MapToDTO).ToList());
        }

        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập GateId", 400, false);

            var gate = await _unitOfWork.GateRepo.GetAll()
                .Include(g => g.Floor)
                .FirstOrDefaultAsync(g => g.GateId == id);

            if (gate == null) return new ResponseDTO("Không tìm thấy cổng", 404, false);
            return new ResponseDTO("Lấy thông tin cổng thành công", 200, true, MapToDTO(gate));
        }

        public async Task<ResponseDTO> CreateAsync(CreateGateDTO dto)
        {
            if (dto == null) return new ResponseDTO("Dữ liệu tạo cổng không hợp lệ", 400, false);

            var validation = await ValidateGateAsync(dto.GateName, dto.GateType, dto.FloorId, null);
            if (validation.Error != null) return validation.Error;

            var gate = new Gate
            {
                GateId = Guid.NewGuid(),
                GateName = dto.GateName.Trim(),
                GateType = validation.GateType!,
                FloorId = dto.FloorId
            };

            await _unitOfWork.GateRepo.AddAsync(gate);
            await _unitOfWork.SaveChangeAsync();
            gate.Floor = validation.Floor;

            return new ResponseDTO("Tạo cổng thành công", 201, true, MapToDTO(gate));
        }

        public async Task<ResponseDTO> UpdateAsync(UpdateGateDTO dto)
        {
            if (dto == null || dto.GateId == Guid.Empty) return new ResponseDTO("Dữ liệu cập nhật cổng không hợp lệ", 400, false);

            var gate = await _unitOfWork.GateRepo.GetByIdAsync(dto.GateId);
            if (gate == null) return new ResponseDTO("Không tìm thấy cổng", 404, false);

            var validation = await ValidateGateAsync(dto.GateName, dto.GateType, dto.FloorId, dto.GateId);
            if (validation.Error != null) return validation.Error;

            gate.GateName = dto.GateName.Trim();
            gate.GateType = validation.GateType!;
            gate.FloorId = dto.FloorId;

            await _unitOfWork.GateRepo.UpdateAsync(gate);
            await _unitOfWork.SaveChangeAsync();
            gate.Floor = validation.Floor;

            return new ResponseDTO("Cập nhật cổng thành công", 200, true, MapToDTO(gate));
        }

        public async Task<ResponseDTO> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty) return new ResponseDTO("Vui lòng nhập GateId", 400, false);

            var gate = await _unitOfWork.GateRepo.GetByIdAsync(id);
            if (gate == null) return new ResponseDTO("Không tìm thấy cổng", 404, false);

            try
            {
                _unitOfWork.GateRepo.Delete(gate);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO("Xóa cổng thành công", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi xóa cổng: {ex.Message}", 500, false);
            }
        }

        private async Task<(Floor? Floor, string? GateType, ResponseDTO? Error)> ValidateGateAsync(string? gateName, string? gateType, Guid floorId, Guid? currentGateId)
        {
            if (string.IsNullOrWhiteSpace(gateName)) return (null, null, new ResponseDTO("Vui lòng nhập tên cổng", 400, false));
            if (floorId == Guid.Empty) return (null, null, new ResponseDTO("Vui lòng chọn tầng", 400, false));

            var normalizedGateType = NormalizeGateType(gateType);
            if (normalizedGateType == null) return (null, null, new ResponseDTO("GateType chỉ được là Entry hoặc Exit", 400, false));

            var floor = await _unitOfWork.FloorRepo.GetByIdAsync(floorId);
            if (floor == null) return (null, null, new ResponseDTO("Tầng không tồn tại", 400, false));

            var name = gateName.Trim().ToLower();
            var duplicate = await _unitOfWork.GateRepo.GetAll()
                .AnyAsync(g => g.GateName.ToLower() == name && (!currentGateId.HasValue || g.GateId != currentGateId.Value));
            if (duplicate) return (null, null, new ResponseDTO("Tên cổng đã tồn tại", 400, false));

            return (floor, normalizedGateType, null);
        }

        private static string? NormalizeGateType(string? gateType)
        {
            if (string.IsNullOrWhiteSpace(gateType)) return null;
            if (string.Equals(gateType.Trim(), "Entry", StringComparison.OrdinalIgnoreCase)) return "Entry";
            if (string.Equals(gateType.Trim(), "Exit", StringComparison.OrdinalIgnoreCase)) return "Exit";
            return null;
        }

        private static GateDTO MapToDTO(Gate gate)
        {
            return new GateDTO
            {
                GateId = gate.GateId,
                GateName = gate.GateName,
                GateType = gate.GateType,
                FloorId = gate.FloorId,
                FloorName = gate.Floor?.FloorName
            };
        }
    }
}
