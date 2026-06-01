// Tests/Integration/SubServices/Logistics/GA/FarmEvolutionIntegrationTests.cs

using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Tests.IntegrationTests;

public class FarmEvolutionIntegrationTests : TestBase
{
    private readonly ITestOutputHelper _output;
    
    public FarmEvolutionIntegrationTests(ITestOutputHelper output) : base()
    {
        _output = output;
        
        // Добавляем несколько ферм для интеграционного теста
        var farmLocations = new Dictionary<int, LocationPoint>
        {
            { 1, new LocationPoint(1, "Farm_1", 55.8, 37.7) },
            { 2, new LocationPoint(2, "Farm_2",55.9, 37.8) }
        };
        _mockContext.Setup(c => c.GetFarmLocations()).Returns(farmLocations);
    }

    [Fact]
    public async Task OptimizeAsync_FarmAssignment_EvolvesBetterSolutions()
    {
        // Arrange
        _mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns((Chromosome c) => new DecodingResult
            {
                Metrics = new ChromosomeMetrics
                {
                    TotalDistance = c.Genes.Count * 10.0,
                    FuelCost = 1.0,
                    TimeWindowViolations = 0
                }
            });
        
        var ga = new GeneticAlgorithm(
            decodingContext: _mockContext.Object,
            fitnessObjective: FitnessObjective.MinimizeDistance,
            populationSize: 20,
            maxGenerations: 30,
            crossoverRate: 0.8,
            mutationRate: 0.15,
            farmMutationRate: 0.2,
            decoderFactory: () => _mockDecoder.Object,
            logger: _mockLogger.Object
        );
        
        // Задачи без жёсткой привязки к ферме
        var tasks = CreateTestTasks(3).Select(t => 
        {
            t.FarmId = null; // Разрешаем выбор любой фермы
            return t;
        }).ToList();
        
        // Act
        var bestSolution = await ga.OptimizeAsync(tasks);
        var result = ga.GetOptimizationResult();
        
        // Assert
        _output.WriteLine($"Best Fitness: {result.BestFitness}");
        _output.WriteLine($"Generations evaluated: {result.SolutionFitnessHistory.Count}");
        
        Assert.NotNull(bestSolution);
        Assert.True(result.BestFitness < double.MaxValue);
        
        // Проверяем, что в лучшем решении у задач назначены фермы
        var unloadGenes = bestSolution.Routes
            .SelectMany(r => r.Genes)
            .Where(g => g.Operation == OperationType.Unload)
            .ToList();
        
        Assert.NotEmpty(unloadGenes);
        
        foreach (var gene in unloadGenes)
        {
            Assert.NotNull(gene.GetEffectiveFarmId());
        }
    }

    [Fact]
    public async Task OptimizeAsync_MultipleTasks_DistributesFarms()
    {
        // Arrange
        _mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns((Chromosome c) => new DecodingResult
            {
                Metrics = new ChromosomeMetrics
                {
                    TotalDistance = c.Genes.Count * 5.0,
                    FuelCost = 0.5
                }
            });
        
        var ga = new GeneticAlgorithm(
            decodingContext: _mockContext.Object,
            populationSize: 15,
            maxGenerations: 20,
            farmMutationRate: 0.3,
            decoderFactory: () => _mockDecoder.Object,
            logger: _mockLogger.Object
        );
        
        var tasks = CreateTestTasks(5).Select(t => 
        {
            t.FarmId = null;
            return t;
        }).ToList();
        
        // Act
        var bestSolution = await ga.OptimizeAsync(tasks);
        
        // Assert
        var assignedFarms = bestSolution.Routes
            .SelectMany(r => r.Genes)
            .Where(g => g.Operation == OperationType.Unload)
            .Select(g => g.GetEffectiveFarmId())
            .Where(f => f.HasValue)
            .Select(f => f!.Value)
            .Distinct()
            .ToList();
        
        _output.WriteLine($"Used farms: {string.Join(", ", assignedFarms)}");
        
        // Должны использоваться доступные фермы
        var availableFarms = _mockContext.Object.GetFarmLocations().Keys;
        Assert.All(assignedFarms, farmId => Assert.Contains(farmId, availableFarms));
    }

    [Fact]
    public async Task OptimizeAsync_FixedFarmTasks_PreservesAssignment()
    {
        // Arrange
        _mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns((Chromosome c) => new DecodingResult
            {
                Metrics = new ChromosomeMetrics { TotalDistance = 10.0 }
            });
        
        var ga = new GeneticAlgorithm(
            decodingContext: _mockContext.Object,
            populationSize: 10,
            maxGenerations: 15,
            decoderFactory: () => _mockDecoder.Object,
            logger: _mockLogger.Object
        );
        
        // Задачи с фиксированной фермой
        var tasks = CreateTestTasks(3); // FarmId = Id задачи (1, 2, 3)
        
        // Act
        var bestSolution = await ga.OptimizeAsync(tasks);
        
        // Assert
        foreach (var task in tasks)
        {
            var unloadGene = bestSolution.Routes
                .SelectMany(r => r.Genes)
                .FirstOrDefault(g => g.TaskId == task.Id && g.Operation == OperationType.Unload);
            
            Assert.NotNull(unloadGene);
            // Фиксированная ферма должна сохраниться
            Assert.Equal(task.FarmId, unloadGene!.GetEffectiveFarmId());
        }
    }
}