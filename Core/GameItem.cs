namespace NvidiaCi
{
    public class GameItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? LaunchUri { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string? IconPath { get; set; }
    }
}
