namespace Server.Models
{
    public class Lobby
    {
        public string HostConnectionId { get; set; }
        public string HostName { get; set; } 
        public string? GuestConnectionId { get; set; }
        public string? GuestName { get; set; }
        public bool IsFull => HostConnectionId != null && GuestConnectionId != null;
    }
}
