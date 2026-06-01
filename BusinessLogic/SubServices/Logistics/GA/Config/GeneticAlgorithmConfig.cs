using System.ComponentModel.DataAnnotations;

namespace BusinessLogic.SubServices.Logistics.GA.Config;

public class GeneticAlgorithmConfig
{
    [Range(10, 1000, ErrorMessage = "Значение должно быть от 10 до 1000!")]
    public int PopulationSize { get; set; } = 100;
    [Range(100, 10000)]
    public int MaxGenerations { get; set; } = 300;
    [Range(0.0, 1.0)]
    public double CrossoverRate { get; set; } = 0.8;
    [Range(0.0, 1.0)]
    public double MutationRate { get; set; } = 0.15;
    [Range(2, 100)]
    public int TournamentSize { get; set; } = 5;
    [Range(0.0, 1.0)]
    public double VehicleMutationRate { get; set; } = 0.25;
    [Range(0.0, 1.0)]
    public double FarmMutationRate { get; set; } = 0.2;
}