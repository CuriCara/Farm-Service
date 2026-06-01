using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using Xunit;

namespace Tests.IntegrationTests;

public class InitializePopulationTests : TestBase
{
    [Fact]
    public void InitializePopulation_CreatesCorrectSize()
    {
        // Arrange
        var tasks = CreateTestTasks(5);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 10,
            logger: _mockLogger.Object);

        // Act
        var method = typeof(GeneticAlgorithm).GetMethod(
            "InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result!.Count);
    }

    [Fact]
    public void InitializePopulation_AllSolutionsHaveValidPairs()
    {
        // Arrange
        var tasks = CreateTestTasks(5);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 20,
            logger: _mockLogger.Object);

        // Act
        var method = typeof(GeneticAlgorithm).GetMethod(
            "InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;

        // Assert: каждая хромосома должна иметь Load перед Unload
        foreach (var solution in population!)
        {
            foreach (var route in solution.Routes)
            {
                var loaded = new HashSet<int>();
                foreach (var gene in route.Genes)
                {
                    if (gene.Operation == OperationType.Load)
                    {
                        loaded.Add(gene.TaskId);
                    }
                    else // Unload
                    {
                        Assert.Contains(gene.TaskId, loaded);
                    }
                }
            }
        }
    }

    [Fact]
    public void InitializePopulation_FirstSolutionIsGreedy()
    {
        // Arrange
        var tasks = CreateTestTasks(3);
        tasks[0].Priority = 3.0;
        tasks[1].Priority = 1.0; // Самый высокий приоритет
        tasks[2].Priority = 2.0;
        
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 5,
            logger: _mockLogger.Object);

        // Act
        var method = typeof(GeneticAlgorithm).GetMethod(
            "InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        var firstSolution = population![0];

        // Assert: первый маршрут должен содержать задачи в порядке приоритета
        var allGenes = firstSolution.Routes.SelectMany(r => r.Genes).ToList();
        var firstTaskId = allGenes.First(g => g.Operation == OperationType.Load).TaskId;
        
        Assert.Equal(2, firstTaskId); // Task #2 имеет приоритет 1.0 (лучший)
    }

    [Fact]
    public void InitializePopulation_ExcludesShortageTasks()
    {
        // Arrange
        var tasks = CreateTestTasks(3);
        tasks[1].IsShortage = true; // Вторая задача недоступна
        
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 5,
            logger: _mockLogger.Object);

        // Act
        var method = typeof(GeneticAlgorithm).GetMethod(
            "InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        var solution = population![0];

        // Assert: задача #2 не должна присутствовать
        var allTaskIds = solution.Routes.SelectMany(r => r.Genes.Select(g => g.TaskId)).Distinct();
        Assert.DoesNotContain(2, allTaskIds);
        Assert.Contains(1, allTaskIds);
        Assert.Contains(3, allTaskIds);
    }
}