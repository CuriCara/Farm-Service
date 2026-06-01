namespace BusinessLogic.SubServices.Logistics.Optimization;

public class Chromosome
{
    public int VehicleId { get; set; }
    public List<Gene> Genes { get; set; } = new();
    public double Fitness { get; set; }
    public ChromosomeMetrics Metrics { get; set; } = new();

    public Chromosome(int vehicleId) => VehicleId = vehicleId;

    public Chromosome Clone()
    {
        var clone = new Chromosome(VehicleId)
        {
            Fitness = Fitness,
            Metrics = Metrics?.Clone() ?? new ChromosomeMetrics()
        };
        clone.Genes = Genes.Select(g => g.Clone()).ToList();
        return clone;
    }
}