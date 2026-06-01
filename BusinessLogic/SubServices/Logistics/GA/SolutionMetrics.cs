namespace BusinessLogic.SubServices.Logistics.GA;

public class SolutionMetrics
{
    public int TotalVehiclesUsed { get; set; }
    public double TotalDistance { get; set; }
    public int TotalTimeViolations { get; set; }
    public double TotalFuelCost { get; set; }
    public int AllFineOnSol { get; set; }
    public SolutionMetrics Clone() => (SolutionMetrics)MemberwiseClone();
}