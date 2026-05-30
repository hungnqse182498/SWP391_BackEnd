using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Settings
{
    public class JwtSettingModel
    {
        /// <summary>
        /// The Secret key of the jwt to generate access token.
        /// </summary>
        public static string SecretKey { get; set; } = "qwertyuiopasdfghjklzxcvbnm123456";

        /// <summary>
        /// The expire days of the jwt to generate access token.
        /// </summary>
        public static int ExpireDayAccessToken { get; set; } = 1;

        /// <summary>
        /// The expire days of the jwt to generate refresh token.
        /// </summary>
        public static int ExpireDayRefreshToken { get; set; } = 30;

        /// <summary>
        /// The issuer of the token.
        /// </summary>
        public static string Issuer { get; set; } = "https://localhost:7155"; // Thay đổi cho phù hợp

        /// <summary>
        /// The audience of the token.
        /// </summary>
        public static string Audience { get; set; } = "https://localhost:3000"; // Thay đổi cho phù hợp
    }
}
