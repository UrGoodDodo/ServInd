namespace Server.Dtos
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public Guid UserId { get; set; }

        public string Nickname { get; set; }

        public string AvatarBase64 { get; set; }

        public string AvatarExtension { get; set; }

        public string JwtToken { get; set; }
    }
}
