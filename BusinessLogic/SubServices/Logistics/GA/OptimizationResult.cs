namespace BusinessLogic.SubServices.Logistics.GA;

public class OptimizationResult
{
    public double BestFitness { get; set; }
    public Solution BestSolution { get; set; }
    public List<double> SolutionFitnessHistory { get; set; }
}