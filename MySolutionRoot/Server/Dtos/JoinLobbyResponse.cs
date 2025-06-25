namespace Server.Dtos
{
    public class JoinLobbyResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public string HostNickName { get; set; }

        public string HostAvatarBase64 { get; set; }

        public string HostAvatarFilename { get; set; }

        public string GuestNickName { get; set; }

        public string GuestAvatarBase64 { get; set; }

        public string GuestAvatarFilename { get; set; }
    }
}
