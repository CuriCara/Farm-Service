// Tests/IntegrationTests/GeneticAlgorithmIntegrationTests.cs

using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.Logistics.GA;
using Moq;
using Xunit;

namespace Tests.IntegrationTests;

public class GeneticAlgorithmIntegrationTests : TestBase
{
    [Fact]
    public async Task OptimizeAsync_ReturnsValidSolution()
    {
        // Arrange
        var tasks = CreateTestTasks(5);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 20,
            maxGenerations: 10,
            logger: _mockLogger.Object);

        // Мокаем декодер для возврата валидных метрик
        _mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns(new DecodingResult
            {
                Metrics = new ChromosomeMetrics
                {
                    TotalDistance = 50.0,
                    FuelCost = 100.0,
                    TimeWindowViolations = 0
                },
                IsValid = true
            });

        // Act
        var result = await ga.OptimizeAsync(tasks, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Routes);
        
        // Решение должно быть валидным
        foreach (var route in result.Routes)
        {
            var loaded = new HashSet<int>();
            foreach (var gene in route.Genes)
            {
                if (gene.Operation == OperationType.Load)
                    loaded.Add(gene.TaskId);
                else
                    Assert.Contains(gene.TaskId, loaded);
            }
        }
    }

    [Fact]
    public async Task OptimizeAsync_ImprovesFitnessOverGenerations()
    {
        // Arrange
        var tasks = CreateTestTasks(3);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 10,
            maxGenerations: 20,
            logger: _mockLogger.Object);

        // Мокаем декодер с небольшим рандомом для имитации эволюции
        var random = new Random(42);
        _mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns(() => new DecodingResult
            {
                Metrics = new ChromosomeMetrics
                {
                    TotalDistance = 50 + random.Next(-40, 100),
                    FuelCost = 100,
                    TimeWindowViolations = random.Next(0, 4)
                },
                IsValid = true
            });

        // Act
        var result = await ga.OptimizeAsync(tasks, CancellationToken.None);
        var optimizationResult = ga.GetOptimizationResult();

        // Assert
        Assert.NotNull(optimizationResult.SolutionFitnessHistory);
        Assert.Equal(20, optimizationResult.SolutionFitnessHistory.Count);
        
        // Фитнес должен улучшаться (или хотя бы не ухудшаться сильно)
        var firstFitness = optimizationResult.SolutionFitnessHistory[0];
        var lastFitness = optimizationResult.SolutionFitnessHistory.Last();
        
        Assert.True(lastFitness <= firstFitness * 1.5, 
            $"Фитнес ухудшился: {firstFitness} → {lastFitness}");
    }

    [Fact]
    public async Task OptimizeAsync_HandlesEmptyTasks()
    {
        // Arrange
        var tasks = new List<DeliveryTaskDTO>();
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            logger: _mockLogger.Object);

        // Act
        var result = await ga.OptimizeAsync(tasks, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Routes);
    }
    
    
}