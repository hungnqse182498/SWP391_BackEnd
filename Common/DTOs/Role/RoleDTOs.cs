using System;

namespace Common.DTOs.Role
{
    public class RoleDTO
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CreateRoleDTO
    {
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateRoleDTO
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
