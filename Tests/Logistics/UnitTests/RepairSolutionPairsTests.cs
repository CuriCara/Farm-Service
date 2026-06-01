using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using Xunit;

namespace Tests.UnitTests;

public class RepairSolutionPairsTests : TestBase
{
    [Fact]
    public void RepairSolutionPairs_FixesInterRouteSplit()
    {
        // Arrange: Load в маршруте 1, Unload в маршруте 2
        var tasks = CreateTestTasks(1);
        var solution = new Solution();
        
        var route1 = new Chromosome(1);
        route1.Genes.Add(new Gene(1, OperationType.Load, tasks[0]));
        
        var route2 = new Chromosome(2);
        route2.Genes.Add(new Gene(1, OperationType.Unload, tasks[0]));
        
        solution.Routes.Add(route1);
        solution.Routes.Add(route2);

        // Act
        var ga = CreateGeneticAlgorithm();
        InvokeRepairSolutionPairs(ga, solution);

        // Assert: обе операции должны быть в одном маршруте
        var allGenes = solution.Routes.SelectMany(r => r.Genes).ToList();
        var routeWithBoth = solution.Routes.FirstOrDefault(r => r.Genes.Count == 2);
        
        Assert.NotNull(routeWithBoth);
        Assert.Equal(OperationType.Load, routeWithBoth.Genes[0].Operation);
        Assert.Equal(OperationType.Unload, routeWithBoth.Genes[1].Operation);
        Assert.Equal(1, solution.Routes.Count(r => r.Genes.Any())); // Один маршрут с генами
    }

    [Fact]
    public void RepairSolutionPairs_FixesWrongOrder()
    {
        // Arrange: Unload перед Load
        var tasks = CreateTestTasks(1);
        var solution = new Solution();
        var route = new Chromosome(1);
        
        route.Genes.Add(new Gene(1, OperationType.Unload, tasks[0]));
        route.Genes.Add(new Gene(1, OperationType.Load, tasks[0]));
        
        solution.Routes.Add(route);

        // Act
        var ga = CreateGeneticAlgorithm();
        InvokeRepairSolutionPairs(ga, solution);

        // Assert: Load должен быть перед Unload
        var fixedRoute = solution.Routes[0];
        Assert.Equal(OperationType.Load, fixedRoute.Genes[0].Operation);
        Assert.Equal(OperationType.Unload, fixedRoute.Genes[1].Operation);
    }

    [Fact]
    public void RepairSolutionPairs_KeepsValidPairsUnchanged()
    {
        // Arrange: уже валидный порядок
        var tasks = CreateTestTasks(1);
        var solution = new Solution();
        var route = new Chromosome(1);
        
        route.Genes.Add(new Gene(1, OperationType.Load, tasks[0]));
        route.Genes.Add(new Gene(1, OperationType.Unload, tasks[0]));
        
        solution.Routes.Add(route);

        // Act
        var ga = CreateGeneticAlgorithm();
        InvokeRepairSolutionPairs(ga, solution);

        // Assert: порядок не изменился
        Assert.Equal(OperationType.Load, solution.Routes[0].Genes[0].Operation);
        Assert.Equal(OperationType.Unload, solution.Routes[0].Genes[1].Operation);
    }

    private GeneticAlgorithm CreateGeneticAlgorithm()
    {
        return new GeneticAlgorithm(
            _mockContext.Object,
            logger: _mockLogger.Object
        );
    }

    private void InvokeRepairSolutionPairs(GeneticAlgorithm ga, Solution solution)
    {
        // Используем рефлексию для вызова приватного метода
        var method = typeof(GeneticAlgorithm).GetMethod(
            "RepairSolutionPairs", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        method?.Invoke(ga, new object[] { solution });
    }
}