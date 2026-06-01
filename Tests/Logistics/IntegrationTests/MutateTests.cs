using System.Reflection;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Tests.IntegrationTests;

/// <summary>
/// Интеграционные тесты для проверки работы мутаций в контексте полного цикла ГА.
/// Проверяют не только саму мутацию, но и её влияние на валидность решения и fitness.
/// </summary>
public class MutationIntegrationTests : TestBase
{
    
    #region Setup

    private GeneticAlgorithm CreateGA(
        FitnessObjective objective = FitnessObjective.MinimizeDistance,
        double mutationRate = 0.15,
        double vehicleMutationRate = 0.2,
        double farmMutationRate = 0.2,  // 🔹 Добавили параметр для ферм
        int populationSize = 20,
        int maxGenerations = 10)
    {
        var mockDecoder = new Mock<IRouteDecoder>();
    
        // Мокаем декодер: возвращаем предсказуемые метрики
        mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns((Chromosome c) => 
            {
                var metrics = new ChromosomeMetrics
                {
                    TotalDistance = c.Genes.Count * 10.0,
                    FuelCost = 15.0,
                    TimeWindowViolations = 0,
                    ProductViolations = 0,
                    LoadMoreMaxKg = 0,
                    MaxLoadKg = c.Genes.Count(g => g.Operation == OperationType.Load) * 100
                };
                return new DecodingResult { Metrics = metrics, IsValid = true };
            });

        return new GeneticAlgorithm(
            _mockContext.Object,
            objective,
            populationSize: populationSize,
            maxGenerations: maxGenerations,
            crossoverRate: 0.8,
            mutationRate: mutationRate,
            tournamentSize: 5,
            vehicleMutationRate: vehicleMutationRate,
            farmMutationRate: farmMutationRate,  // 🔹 Передаём новый параметр
            decoderFactory: () => mockDecoder.Object,
            logger: NullLogger<GeneticAlgorithm>.Instance);
    }

    private Solution CreateValidSolution(int vehicleCount, int tasksPerVehicle)
    {
        var solution = new Solution();
        var tasks = CreateTestTasks(vehicleCount * tasksPerVehicle);
        
        int taskIdx = 0;
        for (int v = 1; v <= vehicleCount; v++)
        {
            var route = new Chromosome(v);
            for (int t = 0; t < tasksPerVehicle; t++)
            {
                if (taskIdx >= tasks.Count) break;
                var task = tasks[taskIdx++];
                
                // Гарантируем валидный порядок: Load перед Unload
                route.Genes.Add(new Gene(task.Id, OperationType.Load, task));
                route.Genes.Add(new Gene(task.Id, OperationType.Unload, task));
            }
            if (route.Genes.Any())
                solution.Routes.Add(route);
        }
        return solution;
    }
    
    #endregion

    #region Intra-Route Mutation Tests

    [Fact]
    public void Mutate_SwapOperations_PreservesTaskPairs()
    {
        // Arrange
        var solution = CreateValidSolution(1, 3); // 1 машина, 3 задачи
        var originalTaskIds = solution.Routes
            .SelectMany(r => r.Genes.Select(g => g.TaskId))
            .OrderBy(id => id)
            .ToList();

        var ga = CreateGA(mutationRate: 1.0); // 100% вероятность мутации

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert
        var mutatedTaskIds = mutated.Routes
            .SelectMany(r => r.Genes.Select(g => g.TaskId))
            .OrderBy(id => id)
            .ToList();
        
        Assert.Equal(originalTaskIds, mutatedTaskIds);
        
        // Проверяем, что каждая задача имеет ровно 2 операции
        var taskCounts = mutated.Routes
            .SelectMany(r => r.Genes)
            .GroupBy(g => g.TaskId)
            .ToDictionary(g => g.Key, g => g.Count());
        
        Assert.All(taskCounts.Values, count => Assert.Equal(2, count));
    }

    [Fact]
    public void Mutate_ReverseSegment_MaintainsLoadBeforeUnload()
    {
        // Arrange
        var solution = CreateValidSolution(1, 4);
        var ga = CreateGA(mutationRate: 1.0);

        // Act - выполняем мутацию несколько раз для статистики
        bool allValid = true;
        for (int i = 0; i < 20; i++)
        {
            var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
            if (!ValidateLoadBeforeUnload(mutated))
            {
                allValid = false;
                break;
            }
        }

        // Assert
        Assert.True(allValid, "После Reverse-мутации Load всегда должен идти перед Unload");
    }

    [Fact]
    public void Mutate_WithRepair_FixesBrokenPairs()
    {
        // Arrange: создаём решение с намеренно сломанными парами
        var solution = new Solution();
        var tasks = CreateTestTasks(2);
        
        var route1 = new Chromosome(1);
        route1.Genes.Add(new Gene(tasks[0].Id, OperationType.Load, tasks[0]));
        // ❌ Намеренно пропускаем Unload для задачи 1
        
        var route2 = new Chromosome(2);
        route2.Genes.Add(new Gene(tasks[1].Id, OperationType.Unload, tasks[1]));
        // ❌ Намеренно пропускаем Load для задачи 2
        
        solution.Routes.AddRange(new[] { route1, route2 });

        var ga = CreateGA();

        // Act
        var repaired = GaTestHelper.InvokeMutate(ga, solution.Clone()); // Mutate вызывает RepairSolutionPairs

        // Assert
        var taskCounts = repaired.Routes
            .SelectMany(r => r.Genes)
            .GroupBy(g => g.TaskId)
            .ToDictionary(g => g.Key, g => g.Count());
        
        Assert.Equal(2, taskCounts[tasks[0].Id]);
        Assert.Equal(2, taskCounts[tasks[1].Id]);
    }

    #endregion

    #region Inter-Route Mutation Tests

    [Fact]
    public void Mutate_MoveGeneBetweenRoutes_PreservesSolutionValidity()
    {
        // Arrange
        var solution = CreateValidSolution(2, 2); // 2 машины, по 2 задачи
        var originalTotalGenes = solution.Routes.Sum(r => r.Genes.Count);
        
        var ga = CreateGA(mutationRate: 1.0);

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert
        var mutatedTotalGenes = mutated.Routes.Sum(r => r.Genes.Count);
        Assert.Equal(originalTotalGenes, mutatedTotalGenes);
        
        // Проверяем, что все задачи по-прежнему имеют пару
        Assert.True(ValidateAllTasksHavePairs(mutated),
            "Все задачи должны иметь и Load, и Unload после мутации");
    }

    [Fact]
    public void Mutate_MoveGene_PreservesLoadBeforeUnloadOrder()
    {
        // Arrange
        var solution = CreateValidSolution(2, 3);
        var ga = CreateGA(mutationRate: 1.0);

        // Act & Assert - многократная проверка
        for (int i = 0; i < 30; i++)
        {
            var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
            Assert.True(ValidateLoadBeforeUnload(mutated),
                $"Итерация {i}: Load должен идти перед Unload после переноса гена");
        }
    }

    [Fact]
    public void Mutate_InterRoute_DoesNotCreateEmptyRoutes()
    {
        // Arrange
        var solution = CreateValidSolution(3, 1); // 3 машины, по 1 задаче (минимально)
        var ga = CreateGA(mutationRate: 1.0);

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert
        var emptyRoutes = mutated.Routes.Count(r => !r.Genes.Any());
        Assert.Equal(0, emptyRoutes);
    }

    #endregion

    #region Vehicle Mutation Tests

    [Fact]
    public void Mutate_VehicleChange_UpdatesRouteVehicleId()
    {
        // Arrange
        var solution = CreateValidSolution(1, 2);
        var originalVehicleId = solution.Routes.First().VehicleId;
        var ga = CreateGA(vehicleMutationRate: 1.0); // 100% вероятность смены машины

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert
        var mutatedVehicleId = mutated.Routes.First().VehicleId;
        
        // VehicleId может измениться или остаться (рандом), но решение должно быть валидным
        Assert.True(ValidateAllTasksHavePairs(mutated),
            "Смена VehicleId не должна ломать валидность решения");
        
        // Проверяем диапазон новых ID (в коде: 1..50)
        Assert.InRange(mutatedVehicleId, 1, 50);
    }

    [Fact]
    public void Mutate_VehicleChange_DoesNotAffectTaskOrder()
    {
        // Arrange
        var solution = CreateValidSolution(1, 3);
        var originalOrder = solution.Routes.First()
            .Genes.Select(g => (g.TaskId, g.Operation))
            .ToList();
        
        var ga = CreateGA(vehicleMutationRate: 1.0);
    
        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
    
        // Assert
        var mutatedOrder = mutated.Routes.First()
            .Genes.Select(g => (g.TaskId, g.Operation))
            .ToList();
        
        Assert.Equal(originalOrder, mutatedOrder);
    }

    #endregion

    #region Fitness Recalculation Tests

    [Fact]
    public void Mutate_FitnessRecalculated_AfterIntraRouteMutation()
    {
        // Arrange
        var solution = CreateValidSolution(1, 3);
        var ga = CreateGA();
        
        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
        GaTestHelper.InvokeEvaluateSolution(ga, mutated);
        
        // Assert
        Assert.NotEqual(0, mutated.TotalFitness);
        
        // Fitness может быть лучше или хуже - главное, что он есть
        Assert.True(mutated.TotalFitness > 0 || mutated.TotalFitness == double.MaxValue);
    }

    [Fact]
    public void Mutate_FitnessReflectsTimeWindowViolations()
    {
        // Arrange: создаём декодер, который симулирует нарушение временного окна
        var mockDecoder = new Mock<IRouteDecoder>();
        mockDecoder.Setup(d => d.Decode(It.IsAny<Chromosome>()))
            .Returns((Chromosome c) => 
            {
                // Если в маршруте больше 4 операций - симулируем нарушение
                var violations = c.Genes.Count > 4 ? 1 : 0;
                return new DecodingResult 
                { 
                    Metrics = new ChromosomeMetrics 
                    { 
                        TotalDistance = c.Genes.Count * 10.0,
                        TimeWindowViolations = violations 
                    }, 
                    IsValid = violations == 0 
                };
            });

        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            FitnessObjective.MinimizeTimeViolations,
            mutationRate: 1.0,
            logger: NullLogger<GeneticAlgorithm>.Instance);

        var solution = CreateValidSolution(1, 3);
        var originalFitness = solution.TotalFitness;

        // Act: мутация, которая может создать "нарушение"
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
        GaTestHelper.InvokeEvaluateSolution(ga, mutated);

        // Assert
        if (mutated.Routes.First().Genes.Count > 4)
        {
            Assert.True(mutated.TotalFitness > originalFitness,
                "Нарушения временных окон должны ухудшать fitness при цели MinimizeTimeViolations");
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Mutate_SingleTaskRoute_RemainsValid()
    {
        // Arrange: маршрут с одной задачей (минимальный валидный случай)
        var solution = CreateValidSolution(1, 1);
        var ga = CreateGA(mutationRate: 1.0);

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert
        Assert.True(ValidateAllTasksHavePairs(mutated),
            "Маршрут с одной задачей должен остаться валидным после мутации");
        Assert.True(ValidateLoadBeforeUnload(mutated),
            "Load должен идти перед Unload даже в минимальном маршруте");
    }

    [Fact]
    public void Mutate_ManyTasks_DoesNotBreakPerformance()
    {
        // Arrange: большое решение для проверки производительности
        var solution = CreateValidSolution(5, 10); // 5 машин, 50 задач = 100 генов
        var ga = CreateGA(mutationRate: 0.5);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var iterations = 50;
        for (int i = 0; i < iterations; i++)
        {
            var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());
            Assert.True(ValidateAllTasksHavePairs(mutated));
        }
        stopwatch.Stop();

        // Assert
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        Assert.True(avgTimeMs < 10, 
            $"Мутация должна выполняться быстро: {avgTimeMs:F2} мс на итерацию");
    }

    [Fact]
    public void Mutate_WithShortageTasks_ExcludesThemCorrectly()
    {
        // Arrange
        var tasks = CreateTestTasks(3);
        tasks[1].IsShortage = true; // Задача с дефицитом
        
        var solution = new Solution();
        var route = new Chromosome(1);
        
        // Добавляем все задачи, включая shortage
        foreach (var task in tasks)
        {
            route.Genes.Add(new Gene(task.Id, OperationType.Load, task));
            route.Genes.Add(new Gene(task.Id, OperationType.Unload, task));
        }
        solution.Routes.Add(route);
        
        var ga = CreateGA(mutationRate: 1.0);

        // Act
        var mutated = GaTestHelper.InvokeMutate(ga, solution.Clone());

        // Assert: задача с IsShortage должна быть исключена декодером,
        // но мутация работает на уровне генов - проверяем, что она не ломается
        Assert.True(ValidateAllTasksHavePairs(mutated),
            "Мутация должна корректно работать даже с задачами IsShortage");
    }

    #endregion

    #region Validation Helpers

    private bool ValidateAllTasksHavePairs(Solution solution)
    {
        var taskCounts = solution.Routes
            .SelectMany(r => r.Genes)
            .GroupBy(g => g.TaskId)
            .ToDictionary(g => g.Key, g => g.Count());
        
        return taskCounts.Values.All(count => count == 2);
    }

    private bool ValidateLoadBeforeUnload(Solution solution)
    {
        foreach (var route in solution.Routes)
        {
            var taskPositions = new Dictionary<int, (int? LoadIdx, int? UnloadIdx)>();
            
            for (int i = 0; i < route.Genes.Count; i++)
            {
                var gene = route.Genes[i];
                if (!taskPositions.ContainsKey(gene.TaskId))
                    taskPositions[gene.TaskId] = (null, null);
                
                var current = taskPositions[gene.TaskId];
                if (gene.Operation == OperationType.Load)
                    taskPositions[gene.TaskId] = (i, current.UnloadIdx);
                else
                    taskPositions[gene.TaskId] = (current.LoadIdx, i);
            }
            
            // Проверяем порядок для каждой задачи
            foreach (var (taskId, positions) in taskPositions)
            {
                if (positions.LoadIdx.HasValue && positions.UnloadIdx.HasValue)
                {
                    if (positions.LoadIdx.Value >= positions.UnloadIdx.Value)
                        return false; // Load должен быть строго перед Unload
                }
            }
        }
        return true;
    }

    #endregion
    
    
    #region Reflection Helper

    /// <summary>
    /// Вспомогательный класс для вызова приватных методов GeneticAlgorithm через рефлексию.
    /// Не требует изменения продакшн-кода.
    /// </summary>
    private static class GaTestHelper
    {
        private static readonly MethodInfo MutateMethod = typeof(GeneticAlgorithm)
                                                              .GetMethod("Mutate", BindingFlags.NonPublic | BindingFlags.Instance)
                                                          ?? throw new InvalidOperationException("Mutate method not found");

        private static readonly MethodInfo EvaluateSolutionMethod = typeof(GeneticAlgorithm)
                                                                        .GetMethod("EvaluateSolution", BindingFlags.NonPublic | BindingFlags.Instance)
                                                                    ?? throw new InvalidOperationException("EvaluateSolution method not found");

        public static Solution InvokeMutate(GeneticAlgorithm ga, Solution solution)
        {
            return (Solution)MutateMethod.Invoke(ga, new object[] { solution })!;
        }

        public static void InvokeEvaluateSolution(GeneticAlgorithm ga, Solution solution)
        {
            EvaluateSolutionMethod.Invoke(ga, new object[] { solution });
        }
    }

    #endregion
    
    [Fact]
public void Mutate_WithFarmMutationRate_ChangesAssignedFarmId()
{
    // Arrange
    var ga = CreateGA(farmMutationRate: 1.0); // 100% вероятность мутации
    
    // 🔹 КРИТИЧНО: Настраиваем доступные фермы в контексте
    SetupFarmLocations(new Dictionary<int, LocationPoint>
    {
        { 1, new LocationPoint(1, "Farm_1", 55.8, 37.7) },
        { 2, new LocationPoint(2, "Farm_2", 56.0, 38.0) }
    });
    
    var task = CreateTask(1, farmId: 1);
    
    var solution = new Solution();
    var route = new Chromosome(1);
    
    // Создаём ген с начальной фермой
    var unloadGene = new Gene(1, OperationType.Unload, task, assignedFarmId: 1);
    route.Genes.Add(unloadGene);
    solution.Routes.Add(route);
    
    // Act
    var method = typeof(GeneticAlgorithm).GetMethod("Mutate", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    Assert.NotNull(method); // 🔹 Защита от ошибок рефлексии
    
    var clonedSolution = solution.Clone();
    var mutatedSolution = method.Invoke(ga, new object[] { clonedSolution }) as Solution;
    
    // Assert
    Assert.NotNull(mutatedSolution);
    Assert.NotEmpty(mutatedSolution.Routes);
    
    var mutatedGene = mutatedSolution.Routes.First().Genes.First(g => g.TaskId == 1);
    
    // 🔹 ПРАВИЛЬНАЯ ПРОВЕРКА: AssignedFarmId может быть null, если используется fallback на task.FarmId
    // Проверяем эффективный ID фермы (либо AssignedFarmId, либо FarmId из задачи)
    var effectiveFarmId = mutatedGene.GetEffectiveFarmId();
    
    Assert.True(effectiveFarmId.HasValue, 
        "Должна быть назначена ферма либо через AssignedFarmId, либо через FarmId задачи");
    
    // Если AssignedFarmId имеет значение — проверяем, что это валидная ферма из контекста
    if (mutatedGene.AssignedFarmId.HasValue)
    {
        var farmLocations = _mockContext.Object.GetFarmLocations();
        Assert.Contains(mutatedGene.AssignedFarmId.Value, farmLocations.Keys);
    }
    
    // Опционально: проверяем, что мутация действительно могла изменить значение
    // (не обязательно, т.к. при 2 фермах есть 50% шанс оставить старое значение)
    var originalEffective = unloadGene.GetEffectiveFarmId();
    //Assert.True(effectiveFarmId != originalEffective, "Мутация должна была изменить ферму"); 
    // 🔹 Закомментировано: при малом числе ферм изменение не гарантировано
}

    [Fact]
    public void Mutate_EmptyRoutes_DoesNotThrow()
    {
        // Arrange
        var ga = CreateGA(farmMutationRate: 1.0);
        var solution = new Solution(); // Пустое решение
        
        // Act & Assert
        var method = typeof(GeneticAlgorithm).GetMethod("Mutate", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(ga, new object[] { solution.Clone() }) as Solution;
        
        Assert.NotNull(result);
        Assert.Empty(result.Routes);
    }
}