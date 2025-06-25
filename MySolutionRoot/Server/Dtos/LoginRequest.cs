using System.ComponentModel.DataAnnotations;

namespace Server.Dtos
{
    public class LoginRequest
    {
        [Required]
        public string Nickname { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
