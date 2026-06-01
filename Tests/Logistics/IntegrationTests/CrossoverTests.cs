// Tests/IntegrationTests/CrossoverTests.cs
using BusinessLogic.SubServices.Logistics.GA;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.Logistics.GA;
using Xunit;

namespace Tests.IntegrationTests;

public class CrossoverTests : TestBase
{
    [Fact]
    public void Crossover_ProducesValidChildren()
    {
        // Arrange
        var tasks = CreateTestTasks(10);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 10,
            logger: _mockLogger.Object);

        // Создаём двух родителей вручную
        var parent1 = CreateValidSolution(tasks, new[] { 1, 2 });
        var parent2 = CreateValidSolution(tasks, new[] { 3, 4 });

        // Act
        var method = typeof(GeneticAlgorithm).GetMethod(
            "Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Мокаем TournamentSelection для возврата наших родителей
        SetupTournamentSelection(ga, parent1, parent2);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert
        Assert.NotNull(children);
        Assert.Equal(10, children!.Count);

        // Все дети должны быть валидными
        foreach (var child in children)
        {
            AssertValidSolution(child);
        }
    }

    [Fact]
    public void Crossover_PreservesAllTasks()
    {
        // Arrange
        var tasks = CreateTestTasks(5);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 5,
            logger: _mockLogger.Object);

        var parent1 = CreateValidSolution(tasks, new[] { 1, 2, 3, 5, 4 });
        var parent2 = CreateValidSolution(tasks, new[] { 3, 2, 1, 4, 5 });

        // Act
        SetupTournamentSelection(ga, parent1, parent2);
        var method = typeof(GeneticAlgorithm).GetMethod("Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert: все задачи (1,2,3) должны присутствовать в каждом ребёнке
        foreach (var child in children!)
        {
            var allTaskIds = child.Routes.SelectMany(r => r.Genes.Select(g => g.TaskId)).Distinct();
            Assert.Contains(1, allTaskIds);
            Assert.Contains(2, allTaskIds);
            Assert.Contains(3, allTaskIds);
        }
    }

    private Solution CreateValidSolution(List<DeliveryTaskDTO> tasks, int[] taskIds)
    {
        var solution = new Solution();
        var route = new Chromosome(1);

        foreach (var taskId in taskIds)
        {
            var task = tasks.First(t => t.Id == taskId);
            var idx = route.Genes.Count > 0 ? Random.Shared.Next(0, route.Genes.Count + 1) : 0;
            route.Genes.Insert(idx, new Gene(taskId, OperationType.Load, task));
            route.Genes.Insert(Random.Shared.Next(idx + 1, route.Genes.Count + 1),
                new Gene(taskId, OperationType.Unload, task));
        }

        solution.Routes.Add(route);
        return solution;
    }

    private void AssertValidSolution(Solution solution)
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
                else
                {
                    Assert.Contains(gene.TaskId, loaded);
                }
            }
        }
    }

    [Fact]
    public void Crossover_WithMultiRouteParents_PreservesAllTasks()
    {
        // Arrange: создаём 5 задач
        var tasks = CreateTestTasks(5);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 10,
            logger: _mockLogger.Object);

        // 🔹 Parent1: 2 маршрута
        // Route#1: Task1, Task2
        // Route#2: Task3, Task4, Task5
        var parent1 = new Solution();
        var route1_1 = CreateValidRoute(1, tasks, new[] { 1, 2 });
        var route1_2 = CreateValidRoute(2, tasks, new[] { 3, 4, 5 });
        parent1.Routes.Add(route1_1);
        parent1.Routes.Add(route1_2);

        // 🔹 Parent2: 3 маршрута
        // Route#3: Task1, Task3
        // Route#4: Task2, Task5
        // Route#5: Task4
        var parent2 = new Solution();
        var route2_1 = CreateValidRoute(3, tasks, new[] { 1, 3 });
        var route2_2 = CreateValidRoute(4, tasks, new[] { 2, 5 });
        var route2_3 = CreateValidRoute(5, tasks, new[] { 4 });
        parent2.Routes.Add(route2_1);
        parent2.Routes.Add(route2_2);
        parent2.Routes.Add(route2_3);

        // Act
        SetupTournamentSelection(ga, parent1, parent2);
        var method = typeof(GeneticAlgorithm).GetMethod("Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert
        Assert.NotNull(children);
        Assert.Equal(10, children!.Count);

        foreach (var child in children)
        {
            AssertValidSolutionWithAllTasks(child, tasks.Count);
        }
    }

    [Fact]
    public void Crossover_WithMultiRouteParents_CreatesMultipleRoutes()
    {
        // Arrange: много задач для проверки разделения
        var tasks = CreateTestTasks(10);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 5,
            logger: _mockLogger.Object);

        // Parent1: 3 маршрута с примерно равным распределением
        var parent1 = new Solution();
        parent1.Routes.Add(CreateValidRoute(1, tasks, new[] { 1, 2, 3 }));
        parent1.Routes.Add(CreateValidRoute(2, tasks, new[] { 4, 5, 6 }));
        parent1.Routes.Add(CreateValidRoute(3, tasks, new[] { 7, 8, 9, 10 }));

        // Parent2: 4 маршрута с другим распределением
        var parent2 = new Solution();
        parent2.Routes.Add(CreateValidRoute(4, tasks, new[] { 1, 4, 7 }));
        parent2.Routes.Add(CreateValidRoute(5, tasks, new[] { 2, 5, 8 }));
        parent2.Routes.Add(CreateValidRoute(6, tasks, new[] { 3, 6, 9 }));
        parent2.Routes.Add(CreateValidRoute(7, tasks, new[] { 10 }));

        // Act
        SetupTournamentSelection(ga, parent1, parent2);
        var method = typeof(GeneticAlgorithm).GetMethod("Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert: проверяем, что дети имеют несколько маршрутов (не все задачи в одном)
        foreach (var child in children!)
        {
            var activeRoutes = child.Routes.Where(r => r.Genes.Any()).ToList();

            // 🔹 Хотя бы 2 маршрута (не все задачи в одном)
            Assert.True(activeRoutes.Count >= 2,
                $"Ребёнок имеет только {activeRoutes.Count} маршрут(ов), ожидалось >= 2");

            // 🔹 Не больше 6 маршрутов (разумное ограничение)
            Assert.True(activeRoutes.Count <= 6,
                $"Ребёнок имеет {activeRoutes.Count} маршрутов, ожидалось <= 6");

            // 🔹 Проверяем, что нет маршрута со ВСЕМИ задачами
            var maxTasksInRoute = activeRoutes.Max(r => r.Genes.Count / 2);
            Assert.True(maxTasksInRoute < tasks.Count,
                $"Все {tasks.Count} задач в одном маршруте!");
        }
    }

    [Fact]
    public void Crossover_WithMultiRouteParents_MaintainsValidPairs()
    {
        // Arrange
        var tasks = CreateTestTasks(6);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 5,
            logger: _mockLogger.Object);

        var parent1 = new Solution();
        parent1.Routes.Add(CreateValidRoute(1, tasks, new[] { 1, 2, 3 }));
        parent1.Routes.Add(CreateValidRoute(2, tasks, new[] { 4, 5, 6 }));

        var parent2 = new Solution();
        parent2.Routes.Add(CreateValidRoute(3, tasks, new[] { 1, 3, 5 }));
        parent2.Routes.Add(CreateValidRoute(4, tasks, new[] { 2, 4, 6 }));

        // Act
        SetupTournamentSelection(ga, parent1, parent2);
        var method = typeof(GeneticAlgorithm).GetMethod("Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert: каждая задача должна иметь Load ПЕРЕД Unload в каждом маршруте
        foreach (var child in children!)
        {
            foreach (var route in child.Routes.Where(r => r.Genes.Any()))
            {
                var loaded = new HashSet<int>();
                foreach (var gene in route.Genes)
                {
                    if (gene.Operation == OperationType.Load)
                    {
                        // 🔹 Load не должен повторяться
                        Assert.DoesNotContain(gene.TaskId, loaded);
                        loaded.Add(gene.TaskId);
                    }
                    else // Unload
                    {
                        // 🔹 Unload только после соответствующего Load
                        Assert.Contains(gene.TaskId, loaded);
                    }
                }
            }
        }
    }

    [Fact]
    public void Crossover_WithMultiRouteParents_DistributesTasksAcrossRoutes()
    {
        // Arrange: много задач
        var tasks = CreateTestTasks(12);
        var ga = new GeneticAlgorithm(
            _mockContext.Object,
            populationSize: 10,
            logger: _mockLogger.Object);

        var parent1 = new Solution();
        parent1.Routes.Add(CreateValidRoute(1, tasks, new[] { 1, 2, 3, 4 }));
        parent1.Routes.Add(CreateValidRoute(2, tasks, new[] { 5, 6, 7, 8 }));
        parent1.Routes.Add(CreateValidRoute(3, tasks, new[] { 9, 10, 11, 12 }));

        var parent2 = new Solution();
        parent2.Routes.Add(CreateValidRoute(4, tasks, new[] { 1, 5, 9 }));
        parent2.Routes.Add(CreateValidRoute(5, tasks, new[] { 2, 6, 10 }));
        parent2.Routes.Add(CreateValidRoute(6, tasks, new[] { 3, 7, 11 }));
        parent2.Routes.Add(CreateValidRoute(7, tasks, new[] { 4, 8, 12 }));

        // Act
        SetupTournamentSelection(ga, parent1, parent2);
        var method = typeof(GeneticAlgorithm).GetMethod("Crossover",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var children = method?.Invoke(ga, null) as List<Solution>;

        // Assert: задачи должны быть распределены (не все в 1-2 маршрутах)
        var childrenWithGoodDistribution = children!.Count(c =>
        {
            var activeRoutes = c.Routes.Where(r => r.Genes.Any()).ToList();

            // Хорошее распределение: 3-5 маршрутов для 12 задач
            return activeRoutes.Count >= 3 && activeRoutes.Count <= 5;
        });

        // 🔹 Хотя бы 70% детей должны иметь хорошее распределение
        Assert.True(childrenWithGoodDistribution >= children.Count * 0.7,
            $"Только {childrenWithGoodDistribution}/{children.Count} детей имеют хорошее распределение");
    }

    // 🔹 Вспомогательные методы
    private Chromosome CreateValidRoute(int vehicleId, List<DeliveryTaskDTO> tasks, int[] taskIds)
    {
        var route = new Chromosome(vehicleId);

        // 🔹 Сначала все Load
        foreach (var taskId in taskIds)
        {
            var task = tasks.First(t => t.Id == taskId);
            route.Genes.Add(new Gene(taskId, OperationType.Load, task));
        }

        // 🔹 Потом все Unload (в том же порядке)
        foreach (var taskId in taskIds)
        {
            var task = tasks.First(t => t.Id == taskId);
            route.Genes.Add(new Gene(taskId, OperationType.Unload, task));
        }

        return route;
    }

    private void AssertValidSolutionWithAllTasks(Solution solution, int expectedTaskCount)
    {
        // 🔹 Все задачи присутствуют
        var allTaskIds = solution.Routes
            .SelectMany(r => r.Genes.Select(g => g.TaskId))
            .Distinct()
            .ToList();

        Assert.Equal(expectedTaskCount, allTaskIds.Count);

        // 🔹 Каждая задача имеет ровно 2 операции
        foreach (var taskId in allTaskIds)
        {
            var taskGenes = solution.Routes
                .SelectMany(r => r.Genes)
                .Where(g => g.TaskId == taskId)
                .ToList();

            Assert.Equal(2, taskGenes.Count);
            Assert.Contains(taskGenes, g => g.Operation == OperationType.Load);
            Assert.Contains(taskGenes, g => g.Operation == OperationType.Unload);
        }

        // 🔹 В каждом маршруте Load перед Unload
        foreach (var route in solution.Routes.Where(r => r.Genes.Any()))
        {
            var loaded = new HashSet<int>();
            foreach (var gene in route.Genes)
            {
                if (gene.Operation == OperationType.Load)
                {
                    Assert.DoesNotContain(gene.TaskId, loaded);
                    loaded.Add(gene.TaskId);
                }
                else
                {
                    Assert.Contains(gene.TaskId, loaded);
                }
            }
        }
    }

    private void SetupTournamentSelection(GeneticAlgorithm ga, Solution parent1, Solution parent2)
    {
        var field = typeof(GeneticAlgorithm).GetField("_population",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        field?.SetValue(ga, new List<Solution> { parent1, parent2 });
    }
}