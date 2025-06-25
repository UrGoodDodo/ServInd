namespace Server.Models
{
    public class GameSession
    {
        public string GroupName { get; set; }
        public string CurrentPlayerConnectionId { get; set; }
        public int HostHp { get; set; } = 30;
        public int GuestHp { get; set; } = 30;
        public bool IsGameStarted { get; set; } = false;
        public DateTime TurnStartTime { get; set; }
        public int TurnDurationSeconds { get; set; } = 30;
    }
}
