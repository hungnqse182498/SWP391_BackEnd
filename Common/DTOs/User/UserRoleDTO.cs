using System;

namespace Common.DTOs.User
{
    public class UserRoleDTO
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
