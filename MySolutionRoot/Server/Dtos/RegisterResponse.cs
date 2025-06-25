namespace Server.Dtos
{
    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public string Nickname { get; set; }

        public string AvatarBase64 { get; set; }

        public string AvatarExtension { get; set; }
    }
}
