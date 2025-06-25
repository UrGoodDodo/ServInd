using System.ComponentModel.DataAnnotations;

namespace Server.Dtos
{
    public class RegisterRequest
    {
        [Required, MaxLength(50)]
        public string Nickname { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }

        [Required]
        public string AvatarBase64 { get; set; }

        [Required]
        public string AvatarFilename { get; set; }
    }
}
