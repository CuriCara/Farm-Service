using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Entity.Logistics.GA.Runs;

[Table("FitnessHistoryPoint")]
public class FitnessHistoryPoint : BaseEntity
{
    public int OptimizationRunId { get; set; }
    [ForeignKey("OptimizationRunId")]
    public OptimizationRun Run { get; set; }

    public int Generation { get; set; }
    public double Fitness { get; set; }
}