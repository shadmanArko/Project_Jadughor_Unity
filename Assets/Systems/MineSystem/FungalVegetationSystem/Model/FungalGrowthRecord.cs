namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// What a single decorated cell currently holds, indexed by tilemap layer.
    /// </summary>
    /// <remarks>
    /// Keyed by the decorated cell rather than by anchor wall. Removal inverts the
    /// relationship at query time by probing the broken wall's four cardinal neighbours,
    /// which costs four dictionary lookups and no allocation - cheaper than maintaining a
    /// second anchor-to-cells index, since one wall can anchor up to four neighbours.
    /// </remarks>
    public readonly struct FungalGrowthRecord
    {
        public FungalGrowthRecord(
            FungalGrowthLayer layer0,
            FungalGrowthLayer layer1)
        {
            Layer0 = layer0;
            Layer1 = layer1;
        }

        public FungalGrowthLayer Layer0 { get; }
        public FungalGrowthLayer Layer1 { get; }

        public bool HasAny => Layer0.HasGrowth || Layer1.HasGrowth;

        public FungalGrowthLayer GetLayer(int layer) =>
            layer == 0 ? Layer0 : Layer1;

        public FungalGrowthRecord WithoutLayer(int layer) =>
            layer == 0
                ? new FungalGrowthRecord(FungalGrowthLayer.Empty, Layer1)
                : new FungalGrowthRecord(Layer0, FungalGrowthLayer.Empty);
    }
}
