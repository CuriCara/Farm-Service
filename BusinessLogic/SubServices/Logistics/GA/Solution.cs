using BusinessLogic.SubServices.Logistics.Optimization;

namespace BusinessLogic.SubServices.Logistics.GA;

public class Solution
{
    public List<Chromosome> Routes { get; set; } = new();
    public double TotalFitness { get; set; }
    public SolutionMetrics Metrics { get; set; } = new();

    public Solution Clone()
    {
        var clone = new Solution
        {
            TotalFitness = TotalFitness,
            Metrics = Metrics.Clone()
        };
        clone.Routes = Routes.Select(r => r.Clone()).ToList();
        return clone;
    }
}