namespace Systems.MineSystem.Mine.Model
{
    public class Artifact
    {
        public string Id { get; set; }
        public string Material { get; set; }
        public GridPosition Position { get; set; }
        public string CellId { get; set; }
    }
}
