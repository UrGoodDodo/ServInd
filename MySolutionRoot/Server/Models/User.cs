using System;
using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(50)]
        public string Nickname { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        // Храним либо путь к файлу, либо Base64-строку
        public string AvatarUrl { get; set; }
    }
}