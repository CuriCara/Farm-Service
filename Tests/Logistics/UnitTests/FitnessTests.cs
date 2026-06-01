using BusinessLogic.SubServices.Logistics;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.UnitTests;

public class FitnessTests : TestBase
{
    [Fact]
    public void LocalFitness_CalculatesCorrectly_WithValidMetrics()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        chromosome.Metrics = new ChromosomeMetrics
        {
            TotalDistance = 100.0,
            FuelCost = 15.0,
            TimeWindowViolations = 2,
            ProductViolations = 1,
            LoadMoreMaxKg = 0
        };

        // Используем рефлексию или создаём тестовый экземпляр с доступом к приватному методу
        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeLocalFitness(chromosome);

        // Assert: 100*10 + 15*100 + 2*100 + 1*200 + 0*100 = 1000 + 1500 + 200 + 200 = 2900
        Assert.Equal(2900.0, fitness, 1);
    }

    [Fact]
    public void LocalFitness_ReturnsMaxValue_WhenDistanceIsNaN()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        chromosome.Metrics = new ChromosomeMetrics { TotalDistance = double.NaN };

        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeLocalFitness(chromosome);

        // Assert
        Assert.Equal(double.MaxValue, fitness);
    }

    [Fact]
    public void LocalFitness_ReturnsMaxValue_WhenDistanceIsInfinity()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        chromosome.Metrics = new ChromosomeMetrics { TotalDistance = double.PositiveInfinity };

        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeLocalFitness(chromosome);

        // Assert
        Assert.Equal(double.MaxValue, fitness);
    }

    [Fact]
    public void LocalFitness_ReturnsMaxValue_WhenDistanceIsNegative()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        chromosome.Metrics = new ChromosomeMetrics { TotalDistance = -50.0 };

        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeLocalFitness(chromosome);

        // Assert
        Assert.Equal(double.MaxValue, fitness);
    }

    [Fact]
    public void LocalFitness_HandlesZeroMetrics()
    {
        // Arrange
        var chromosome = new Chromosome(1);
        chromosome.Metrics = new ChromosomeMetrics
        {
            TotalDistance = 0,
            FuelCost = 0,
            TimeWindowViolations = 0,
            ProductViolations = 0,
            LoadMoreMaxKg = 0
        };

        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeLocalFitness(chromosome);

        // Assert
        Assert.Equal(0.0, fitness, 1);
    }

    [Fact]
    public void Fitness_MinimizeDistance_CalculatesCorrectly()
    {
        // Arrange
        var solution = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 2,
                TotalDistance = 150.0,
                TotalFuelCost = 2250.0, // 150 км * 15 руб/км
                TotalTimeViolations = 1,
                AllFineOnSol = 3 // productViolations + LoadMoreMaxKg
            }
        };

        var ga = CreateGeneticAlgorithmForTesting(FitnessObjective.MinimizeDistance);
        var fitness = ga.InvokeFitness(FitnessObjective.MinimizeDistance, solution);

        // Assert: 2*100 + 150*20 + 2250*5 + 1*10 + 3*100 = 200 + 3000 + 11250 + 10 + 300 = 14760
        Assert.Equal(14760.0, fitness, 1);
    }

    [Fact]
    public void Fitness_MinimizeDistance_PenalizesViolationsHeavily()
    {
        // Arrange
        var solution1 = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 1,
                TotalDistance = 100.0,
                TotalFuelCost = 1500.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var solution2 = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 1,
                TotalDistance = 90.0, // меньше расстояние
                TotalFuelCost = 1350.0,
                TotalTimeViolations = 5, // но есть нарушения
                AllFineOnSol = 0
            }
        };

        var ga = CreateGeneticAlgorithmForTesting(FitnessObjective.MinimizeDistance);
        var fitness1 = ga.InvokeFitness(FitnessObjective.MinimizeTimeViolations, solution1);
        var fitness2 = ga.InvokeFitness(FitnessObjective.MinimizeTimeViolations, solution2);

        // Assert: решение с нарушениями должно иметь ХУДШИЙ (больший) fitness
        Assert.True(fitness2 > fitness1, "Нарушения временных окон должны ухудшать fitness");
    }

    [Fact]
    public void Fitness_MinimizeVehicles_PrioritizesFewerVehicles()
    {
        // Arrange
        var solution1 = new Solution // 1 машина
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 1,
                TotalDistance = 300.0,
                TotalFuelCost = 4500.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var solution2 = new Solution // 3 машины, но короче маршрут
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 3,
                TotalDistance = 200.0,
                TotalFuelCost = 3000.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var ga = CreateGeneticAlgorithmForTesting(FitnessObjective.MinimizeVehicles);
        var fitness1 = ga.InvokeFitness(FitnessObjective.MinimizeVehicles, solution1);
        var fitness2 = ga.InvokeFitness(FitnessObjective.MinimizeVehicles, solution2);

        // Assert: 1 машина должна выиграть, несмотря на большее расстояние
        // Sol1: 1*1000 + 300*1 + 4500*0.5 = 1000 + 300 + 2250 = 3550
        // Sol2: 3*1000 + 200*1 + 3000*0.5 = 3000 + 200 + 1500 = 4700
        Assert.True(fitness1 < fitness2, "Меньшее количество машин должно давать лучший fitness");
    }

    [Fact]
    public void Fitness_MinimizeTimeViolations_PrioritizesOnTimeDelivery()
    {
        // Arrange
        var solution1 = new Solution // без нарушений
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 2,
                TotalDistance = 250.0,
                TotalFuelCost = 3750.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var solution2 = new Solution // с нарушениями, но короче
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 2,
                TotalDistance = 200.0,
                TotalFuelCost = 3000.0,
                TotalTimeViolations = 3,
                AllFineOnSol = 0
            }
        };

        var ga = CreateGeneticAlgorithmForTesting(FitnessObjective.MinimizeTimeViolations);
        var fitness1 = ga.InvokeFitness(FitnessObjective.MinimizeTimeViolations, solution1);
        var fitness2 = ga.InvokeFitness(FitnessObjective.MinimizeTimeViolations, solution2);

        // Assert: отсутствие нарушений важнее расстояния
        // Sol1: 2*10 + 250*2 + 3750*1 + 0*1000 = 20 + 500 + 3750 = 4270
        // Sol2: 2*10 + 200*2 + 3000*1 + 3*1000 = 20 + 400 + 3000 + 3000 = 6420
        Assert.True(fitness1 < fitness2, "Отсутствие нарушений временных окон должно быть приоритетом");
    }

    [Fact]
    public void Fitness_ReturnsMaxValue_WhenTotalDistanceIsInvalid()
    {
        // Arrange
        var solution = new Solution
        {
            Metrics = new SolutionMetrics { TotalDistance = double.NaN }
        };

        var ga = CreateGeneticAlgorithmForTesting();
        
        foreach (FitnessObjective objective in Enum.GetValues(typeof(FitnessObjective)))
        {
            var fitness = ga.InvokeFitness(objective, solution);
            Assert.Equal(double.MaxValue, fitness);
        }
    }

    [Fact]
    public void Fitness_HandlesEmptySolution()
    {
        // Arrange
        var solution = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 0,
                TotalDistance = 0,
                TotalFuelCost = 0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var ga = CreateGeneticAlgorithmForTesting();
        var fitness = ga.InvokeFitness(FitnessObjective.MinimizeDistance, solution);

        // Assert: 0*100 + 0*20 + 0*5 + 0*10 + 0*100 = 0
        Assert.Equal(0.0, fitness, 1);
    }

    [Fact]
    public void Fitness_AllFineOnSol_IncreasesPenalty()
    {
        // Arrange
        var solution1 = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 1,
                TotalDistance = 100.0,
                TotalFuelCost = 1500.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 0
            }
        };

        var solution2 = new Solution
        {
            Metrics = new SolutionMetrics
            {
                TotalVehiclesUsed = 1,
                TotalDistance = 100.0,
                TotalFuelCost = 1500.0,
                TotalTimeViolations = 0,
                AllFineOnSol = 5 // ProductViolations + LoadMoreMaxKg
            }
        };

        var ga = CreateGeneticAlgorithmForTesting(FitnessObjective.MinimizeDistance);
        var fitness1 = ga.InvokeFitness(FitnessObjective.MinimizeDistance, solution1);
        var fitness2 = ga.InvokeFitness(FitnessObjective.MinimizeDistance, solution2);

        // Assert: разница должна быть 5 * 100 = 500
        Assert.Equal(500.0, fitness2 - fitness1, 1);
    }

    private FitnessTestWrapper CreateGeneticAlgorithmForTesting(
        FitnessObjective objective = FitnessObjective.MinimizeDistance)
    {
        var mockDecoder = new Mock<ThreadLocal<IRouteDecoder>>();
        return new FitnessTestWrapper(
            mockDecoder.Object,
            _mockContext.Object,
            objective,
            populationSize: 10,
            maxGenerations: 5,
            logger: _mockLogger.Object);
    }

    /// <summary>
    /// Обёртка для тестирования приватных методов Fitness
    /// </summary>
    private class FitnessTestWrapper : GeneticAlgorithm
    {
        public FitnessTestWrapper(
            ThreadLocal<IRouteDecoder> decoder,
            IDecodingContext context,
            FitnessObjective objective,
            int populationSize,
            int maxGenerations,
            ILogger<GeneticAlgorithm>? logger = null)
            : base(context, objective, populationSize, maxGenerations, logger: logger)
        {
        }

        public double InvokeLocalFitness(Chromosome chromosome)
        {
            var method = typeof(GeneticAlgorithm)
                .GetMethod("LocalFitness", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (double)method!.Invoke(this, new object[] { chromosome })!;
        }

        public double InvokeFitness(FitnessObjective objective, Solution solution)
        {
            var method = typeof(GeneticAlgorithm)
                .GetMethod("Fitness", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (double)method!.Invoke(this, new object[] { objective, solution })!;
        }
    }
}