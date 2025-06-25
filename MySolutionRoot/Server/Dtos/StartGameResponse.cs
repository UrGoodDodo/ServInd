namespace Server.Dtos
{
    public class StartGameResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string CurrentPlayerConnectionId { get; set; }
        public string GroupName { get; set; }
    }
}
