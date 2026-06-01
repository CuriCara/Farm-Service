// Tests/Unit/SubServices/Logistics/GA/GeneticAlgorithmFarmTests.cs
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Moq;
using Xunit;

namespace Tests.UnitTests;

public class GeneticAlgorithmFarmTests : TestBase
{
    private GeneticAlgorithm CreateGA(
        int populationSize = 10, 
        int maxGenerations = 5,
        double farmMutationRate = 0.2) => new GeneticAlgorithm(
        decodingContext: _mockContext.Object,
        fitnessObjective: FitnessObjective.MinimizeDistance,
        populationSize: populationSize,
        maxGenerations: maxGenerations,
        crossoverRate: 0.8,
        mutationRate: 0.15,
        tournamentSize: 5,
        vehicleMutationRate: 0.2,
        farmMutationRate: farmMutationRate,
        decoderFactory: () => _mockDecoder.Object,
        logger: _mockLogger.Object
    );
    
    [Fact]
    public void InitializePopulation_UnloadGenes_HaveAssignedFarmId()
    {
        // Arrange
        var ga = CreateGA(populationSize: 5);
        var tasks = CreateTestTasks(3);
        
        // Act
        var method = typeof(GeneticAlgorithm).GetMethod("InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        
        // Assert
        Assert.NotNull(population);
        Assert.Equal(5, population?.Count);
        
        foreach (var solution in population!)
        {
            foreach (var route in solution.Routes)
            {
                var unloadGenes = route.Genes.Where(g => g.Operation == OperationType.Unload).ToList();
                foreach (var gene in unloadGenes)
                {
                    // У каждого Unload-гена должна быть назначена ферма
                    Assert.NotNull(gene.GetEffectiveFarmId());
                    
                    // Ферма должна быть из списка доступных (в тесте это ферма с Id=1)
                    var farmLocations = _mockContext.Object.GetFarmLocations();
                    
                    Assert.NotNull(gene.GetEffectiveFarmId());
                    Assert.True(farmLocations.ContainsKey(gene.GetEffectiveFarmId()!.Value));
                }
            }
        }
    }

    [Fact]
    public void InitializePopulation_FixedFarmId_PreservesTaskFarmId()
    {
        // Arrange
        var ga = CreateGA(populationSize: 3);
        // Создаём задачи с фиксированной фермой
        var tasks = new List<DeliveryTaskDTO>
        {
            CreateTask(1, farmId: 1),
            CreateTask(2, farmId: 1)
        };
        
        var method = typeof(GeneticAlgorithm).GetMethod("InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        
        // Assert
        foreach (var solution in population!)
        {
            var unloadGenes = solution.Routes
                .SelectMany(r => r.Genes)
                .Where(g => g.Operation == OperationType.Unload)
                .ToList();
            
            foreach (var gene in unloadGenes)
            {
                // Если задача имеет жёсткую привязку - она должна сохраниться
                Assert.Equal(1, gene.GetEffectiveFarmId());
            }
        }
    }

    [Fact]
    public void InitializePopulation_Diversity_DifferentFarmAssignments()
    {
        // Arrange - добавляем несколько ферм в контекст для разнообразия
        var farmLocations = new Dictionary<int, LocationPoint>
        {
            { 1, new LocationPoint(1, "Farm_1" , 55.8, 37.7) },
            { 2, new LocationPoint(2, "Farm_2" , 55.9, 37.8) },
            { 3, new LocationPoint(3, "Farm_3" , 56.0, 37.9) }
        };
        _mockContext.Setup(c => c.GetFarmLocations()).Returns(farmLocations);
        
        var ga = CreateGA(populationSize: 20);
        var tasks = CreateTestTasks(5);
        
        var method = typeof(GeneticAlgorithm).GetMethod("InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        
        // Assert - проверяем разнообразие назначений ферм
        var allUnloadFarms = population!
            .SelectMany(s => s.Routes)
            .SelectMany(r => r.Genes)
            .Where(g => g.Operation == OperationType.Unload)
            .Select(g => g.GetEffectiveFarmId())
            .Where(f => f.HasValue)
            .Select(f => f!.Value)
            .ToList();
        
        // Должны использоваться разные фермы (не все одинаковые)
        var uniqueFarms = allUnloadFarms.Distinct().Count();
        Assert.True(uniqueFarms > 1, 
            $"Ожидается разнообразие в назначениях ферм при инициализации. Найдено уникальных ферм: {uniqueFarms}");
    }

    [Fact]
    public void InitializePopulation_NoFarmsInContext_AssignsFromTaskFarmId()
    {
        // Arrange - очищаем фермы в контексте
        _mockContext.Setup(c => c.GetFarmLocations()).Returns(new Dictionary<int, LocationPoint>());
        
        var ga = CreateGA(populationSize: 3);
        var tasks = new List<DeliveryTaskDTO>
        {
            CreateTask(1, farmId: 1), // Задача с фиксированной фермой
            CreateTask(2, farmId: null) // Задача без фермы
        };
        
        var method = typeof(GeneticAlgorithm).GetMethod("InitializePopulation", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var population = method?.Invoke(ga, new object[] { tasks }) as List<Solution>;
        
        // Assert
        var solution = population!.First();
        var genes = solution.Routes.SelectMany(r => r.Genes).ToList();
        
        // Задача с фиксированной фермой должна её сохранить
        var fixedTaskGene = genes.First(g => g.TaskId == 1 && g.Operation == OperationType.Unload);
        Assert.Equal(1, fixedTaskGene.GetEffectiveFarmId());
        
        // Задача без фермы и без доступных ферм в контексте может остаться без фермы
        // (это допустимое поведение, декодер обработает)
        var freeTaskGene = genes.FirstOrDefault(g => g.TaskId == 2 && g.Operation == OperationType.Unload);
        // Не падаем, если ферма не назначена - это валидное состояние для дальнейшей обработки
        Assert.NotNull(freeTaskGene);
    }
}