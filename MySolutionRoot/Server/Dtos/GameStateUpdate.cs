namespace Server.Dtos
{
    public class GameStateUpdate
    {
        public string Message { get; set; }
        public int HostHp { get; set; }
        public int GuestHp { get; set; }
        public string CurrentPlayerNickName { get; set; }
    }
}
