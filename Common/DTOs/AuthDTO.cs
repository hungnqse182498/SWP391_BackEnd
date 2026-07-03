using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class AuthDTO
    {
        public class LoginDTO
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
        public class RegisterDTO
        {
            public string UserName { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string Password { get; set; }
            public string ConfirmPassword { get; set; }
        }

        public class RequestOtpDTO
        {
            public string Email { get; set; }
        }

        public class VerifyRegisterOtpDTO
        {
            public string Email { get; set; }
            public string Otp { get; set; }
        }

        public class VerifyResetPasswordOtpDTO
        {
            public string Email { get; set; }
            public string Otp { get; set; }
            public string NewPassword { get; set; }
            public string ConfirmPassword { get; set; }
        }
    }
} 
