using System.Collections.Concurrent;
using BusinessLogic.SubServices.Logistics.DTO;
using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity;
using DataAccess.Entity.GrH;
using DataAccess.Entity.Logistics.GA;
using DataAccess.Entity.Logistics.GA.Chromosome;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.SubServices.Logistics.GA;

public class GeneticAlgorithm : IGeneticAlgorithm, IDisposable
{
    // Зависимости
    private readonly ThreadLocal<IRouteDecoder> _decoder;
    private readonly ILogger<GeneticAlgorithm>? _logger;
    private readonly IDecodingContext _decodingContext;
    private readonly FitnessObjective _fitnessObjective;
    private readonly ParallelOptions _parallelOptions;
    private readonly SubMethodForPair _pairMethod = new SubMethodForPair();
    private readonly int RSeed;
    private readonly ThreadLocal<Random> _threadRandom;
    private readonly Random _masterRandom;
    public Random R => _threadRandom.Value!;
    
    private readonly int _populationSize;
    private readonly int _maxGenerations;
    private readonly double _crossoverRate;
    private readonly double _mutationRate;
    private readonly int _tournamentSize;
    private readonly double _farmMutationRate;
    private readonly double _vehicleMutationRate;
    
    private List<Solution> _population = new();
    private Solution _bestSolution = new();
    private double _bestFitness = double.MaxValue;
    private readonly List<double> _fitnessHistory = new();

    public GeneticAlgorithm(
        IDecodingContext decodingContext,
        FitnessObjective fitnessObjective = FitnessObjective.MinimizeDistance,
        int populationSize = 50,
        int maxGenerations = 100,
        double crossoverRate = 0.8,
        double mutationRate = 0.15,
        int tournamentSize = 5,
        double vehicleMutationRate = 0.2,
        int maxDegreeOfParallelism = 3,
        double farmMutationRate = 0.2,
        int? randomSeed = null,
        Func<IRouteDecoder>? decoderFactory = null,
        ILogger<GeneticAlgorithm>? logger = null)
    {
        _decodingContext = decodingContext ?? throw new ArgumentNullException(nameof(decodingContext));
        _fitnessObjective = fitnessObjective;
        _populationSize = populationSize;
        _maxGenerations = maxGenerations;
        _crossoverRate = crossoverRate;
        _mutationRate = mutationRate;
        _tournamentSize = tournamentSize;
        _vehicleMutationRate = vehicleMutationRate;
        _farmMutationRate = farmMutationRate;
        RSeed = randomSeed ?? Random.Shared.Next();
        _masterRandom = new Random(RSeed);
        _threadRandom = new ThreadLocal<Random>(() =>
        {
            lock (_masterRandom)
            {
                return new Random(_masterRandom.Next());
            }
        });
        _decoder = new ThreadLocal<IRouteDecoder>(() => 
            decoderFactory?.Invoke() ?? new RouteDecoder(_decodingContext, _logger));
        _parallelOptions = new ParallelOptions 
        { 
            MaxDegreeOfParallelism = maxDegreeOfParallelism 
        };
        _logger = logger;
    }

    // Главный метод для оптимизации
    public async Task<Solution> OptimizeAsync(List<DeliveryTaskDTO> tasks,
        CancellationToken cancellationToken = default)
    {
        if (tasks == null || !tasks.Any())
        {
            _logger?.LogInformation("Нет заданий для оптимизации.");
            return new Solution();
        }
        
        _logger?.LogInformation("Начало работы GA с целью: {Objective}", _fitnessObjective);

        // Инициализация популяции
        _population = InitializePopulation(tasks);
        
        // Оценка особей
        EvaluatePopulation(_population);

        // Лучшая среди первого поколения
        _bestSolution = _population.OrderBy(p => p.TotalFitness).First().Clone();
        _bestFitness = _bestSolution.TotalFitness;

        for (int gen = 0; gen < _maxGenerations; gen++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Новое поколение = старое поколение + скрещенные + мутации родителей + мутации детей (выбираем только 1 треть из них) 
            var newPopulation = new List<Solution>(_populationSize * 4);
            var children = new List<Solution>(_populationSize);
            
            newPopulation.AddRange(_population);
            children = Crossover();
            newPopulation.AddRange(children);
            
            //// Мутация родителей — ПАРАЛЛЕЛЬНО
            // var parentMutation = new ConcurrentBag<Solution>();
            //  Parallel.ForEach(_population, _parallelOptions, obj =>
            //  {
            //      parentMutation.Add(Mutate(obj.Clone()));
            //  });
            //  newPopulation.AddRange(parentMutation);
            
            // Детерминированные параллельные заполнения коллекции
            var parentMutation = new Solution[_populationSize];
            Parallel.For(0, _populationSize, _parallelOptions, i =>
            {
                parentMutation[i] = Mutate(_population[i].Clone());
            });
            newPopulation.AddRange(parentMutation);

            //// Мутация детей 
            // var childrenMutation = new ConcurrentBag<Solution>();
            //  Parallel.ForEach(children, _parallelOptions, obj =>
            //  {
            //      childrenMutation.Add(Mutate(obj.Clone()));
            //  });
            //  newPopulation.AddRange(childrenMutation);

            var childrenMutation = new Solution[_populationSize];
            Parallel.For(0, _populationSize, _parallelOptions, i =>
            {
                childrenMutation[i] = Mutate(children[i].Clone());
            });
            newPopulation.AddRange(childrenMutation);

            // Оценка всех особей
            EvaluatePopulation(newPopulation);

            // Удаляем невалидные особи, если такие появились (уже не актуально)
            // foreach (var pop in newPopulation)
            // {
            //     if (pop.TotalFitness == 100)
            //     {
            //         pop.TotalFitness = double.MaxValue;
            //     }
            // }

            _population = newPopulation
                .DistinctBy(HashSolution) // Убираем дубликаты 
                .OrderBy(s => s.TotalFitness)
                .ThenBy(s => HashSolution(s)) // Детерминированный порядок при равных fitness
                .Take(_populationSize)
                .Select(s => s.Clone()) // Клонируем при отборе
                .ToList();
            
            // Лучшее текущее решение
            var currentBest = _population.MinBy(s => s.TotalFitness);
            if (currentBest.TotalFitness < _bestFitness)
            {
                _bestSolution = currentBest.Clone();
                _bestFitness = currentBest.TotalFitness;
            }
            
            // Сохраняем историю фитнесов 
            _fitnessHistory.Add(_bestFitness);

            
            if (gen % 10 == 0 || gen == _maxGenerations - 1)
                _logger?.LogInformation("Поколение {Gen}: лучший Fitness = {Fitness}", gen, _bestFitness);

            // Остановка при стагнации
            if (gen > 20 && _fitnessHistory.SkipLast(20).All(f => Math.Abs(f - _bestFitness) < 1e-5))
            {
                _logger?.LogInformation("Прогресс остановлен. Найдено лучшее решение.");
                break;
            }
        }
        
        _logger?.LogInformation("GA завершена. Итоговый Fitness: {Best}", _bestFitness);
        return _bestSolution;
    }

    // Возвращаем лучшее решение
    public OptimizationResult GetOptimizationResult() => new()
    {
        BestFitness = _bestFitness,
        BestSolution = _bestSolution,
        SolutionFitnessHistory = _fitnessHistory.ToList() // Создаём копию, чтобы Dispose() не очистил результат
    };

    private List<Solution> InitializePopulation(List<DeliveryTaskDTO> tasks)
    {
        var population = new List<Solution>(_populationSize);
        var vehiclePool = GetActiveVehicleIds();

        if (vehiclePool.Count == 0)
        {
            _logger?.LogInformation("Нету доступных машин!");
            throw new InvalidOperationException("Нету доступных машин!");
        }

        for (int i = 0; i < _populationSize; i++)
        {
            var solution = new Solution();
            var availableVehicles = vehiclePool;

            // Создаём ПАРЫ операций для каждой задачи (исключаем дефицит)
            var taskPairs = ExpandTasksToOperations(tasks);

            if (i == 0) // Жадное первое решение
            {
                // Сортируем пары по приоритету задачи
                var sortedPairs = taskPairs.OrderBy(t => t.Item1.Task.Priority).ToList();

                int vIdx = 0;
                foreach (var (load, unload, priority) in sortedPairs)
                {
                    if (vIdx >= availableVehicles.Count) vIdx = 0;

                    var route = solution.Routes.FirstOrDefault(r => r.VehicleId == availableVehicles[vIdx]);
                    if (route == null)
                    {
                        route = new Chromosome(availableVehicles[vIdx]);
                        solution.Routes.Add(route);
                    }

                    // Гарантируем порядок: Load всегда перед Unload
                    route.Genes.Add(load);
                    route.Genes.Add(unload);
                    vIdx++;
                }
            }
            else // Случайное распределение с гарантией валидности
            {
                // Перемешиваем ПАРЫ, а не отдельные операции
                var shuffledPairs = taskPairs.OrderBy(_ => R.Next()).ToList();

                var shuffledVehicles = vehiclePool
                    .OrderBy(_ => R.Next())
                    .ToList();

                int maxVehiclesToUse = Math.Min(vehiclePool.Count, tasks.Count);
                int vehicleUsed = R.Next(1, maxVehiclesToUse + 1);

                var selectedVehicles = shuffledVehicles
                    .Take(vehicleUsed)
                    .ToList();

                foreach (var (load, unload, priority) in shuffledPairs)
                {
                    var targetVehicle = selectedVehicles[R.Next(selectedVehicles.Count)];
                    var route = solution.Routes.FirstOrDefault(r => r.VehicleId == targetVehicle);
                    if (route == null)
                    {
                        route = new Chromosome(targetVehicle);
                        solution.Routes.Add(route);
                    }

                    // Load, затем Unload после него
                    // Это даёт больше разнообразия при сохранении валидности
                    int loadPos = R.Next(route.Genes.Count + 1);
                    route.Genes.Insert(loadPos, load);

                    // Unload встает в любую позицию строго после Load
                    int unloadPos = R.Next(loadPos + 1, route.Genes.Count + 1);
                    route.Genes.Insert(unloadPos, unload);
                }
            }

            population.Add(solution);
        }

        return population;
    }

    // Разбиение на задачи загрузки/разгрузки
    private List<(Gene, Gene, double)>? ExpandTasksToOperations(List<DeliveryTaskDTO> tasks)
    {
        // Получаем список всех доступных ферм из контекста
        var allFarms = _decodingContext.GetFarmLocations()?.Values.Select(f => f.Index).ToList();

        var ops = tasks
            .Select(t => 
            {
                // Для Unload: случайная ферма (если в задаче не задана)
                int? loadFarmId = 1;
            
                if (t.FarmId.HasValue)
                {
                    // Задача привязана к конкретной ферме — используем её
                    loadFarmId = t.FarmId.Value;
                }
                else if (allFarms.Any())
                {
                    // Случайная ферма из доступных — часть генома для эволюции
                    loadFarmId = allFarms[R.Next(allFarms.Count)];
                }

                return (
                    Load : new Gene(t.Id, OperationType.Load, t, loadFarmId),
                    Unload : new Gene(t.Id, OperationType.Unload, t, null),
                    Priority : t.Priority
                );
            })
            .ToList();

        return ops;
    }

    // Оценка для всей популяции
    private void EvaluatePopulation(List<Solution> population)
    {
        Parallel.ForEach(population, _parallelOptions, solution =>
        {
            EvaluateSolution(solution);
        });
    }

    // Оценка каждого решения отельно
    private void EvaluateSolution(Solution solution)
    {
        double totalDistance = 0;
        int totalViolations = 0;
        double totalFuel = 0;
        int usedVehicles = 0;
        int productViolations = 0;
        int LoadMoreMaxKg = 0;

        // Оценка каждой хромосомы отдельно 
        foreach (var route in solution.Routes.Where(route => route.Genes.Any()))
        {
            usedVehicles++;

            var decoder = _decoder.Value;
            var decoderResult = decoder.Decode(route);

            route.Metrics = decoderResult.Metrics ?? new ChromosomeMetrics();
            route.Fitness = LocalFitness(route) == 0 ? double.MaxValue : LocalFitness(route);

            totalDistance += route.Metrics.TotalDistance;
            totalFuel += route.Metrics.FuelCost;
            totalViolations += route.Metrics.TimeWindowViolations;
            productViolations += route.Metrics.ProductViolations;
            LoadMoreMaxKg += route.Metrics.LoadMoreMaxKg;
        }

        solution.Metrics = new SolutionMetrics()
        {
            TotalDistance = totalDistance,
            TotalVehiclesUsed = usedVehicles,
            TotalTimeViolations = totalViolations,
            TotalFuelCost = totalFuel,
            AllFineOnSol = productViolations + LoadMoreMaxKg
        };

        solution.TotalFitness = Fitness(_fitnessObjective, solution);
    }

    // Fitness для отдой хромосомы
    private double LocalFitness(Chromosome chromosome)
    {
        
        var metrics = chromosome.Metrics;
        
        if (double.IsNaN(metrics.TotalDistance) || double.IsInfinity(metrics.TotalDistance) || metrics.TotalDistance < 0)
            return double.MaxValue;
        
        return metrics.TotalDistance * 20.0 
               + metrics.FuelCost 
               + metrics.TimeWindowViolations * 5000 
               + metrics.ProductViolations * 2000
               + metrics.LoadMoreMaxKg * 1000;
    }

    // Fitness всего решения 
    private double Fitness(FitnessObjective objective, Solution solution)
    {
        var metrics = solution.Metrics;
        
        if (double.IsNaN(metrics.TotalDistance) || double.IsInfinity(metrics.TotalDistance) || metrics.TotalDistance < 0)
            return double.MaxValue;
        
        return objective switch
        {
            FitnessObjective.MinimizeVehicles => metrics.TotalVehiclesUsed * 1000.0 + metrics.TotalDistance * 1.0 +
                                                 metrics.TotalFuelCost * 0.5 +
                                                 metrics.TotalTimeViolations * 2000.0 +
                                                 metrics.AllFineOnSol * 500.0,
            FitnessObjective.MinimizeDistance => metrics.TotalVehiclesUsed * 100.0 + metrics.TotalDistance * 10.0 +
                                                 metrics.TotalFuelCost * 3.5 +
                                                 metrics.TotalTimeViolations * 3000.0 +
                                                 metrics.AllFineOnSol * 1000.0,
            FitnessObjective.MinimizeTimeViolations => metrics.TotalVehiclesUsed * 10.0 + metrics.TotalDistance * 2.0 +
                                                       metrics.TotalFuelCost * 1.0 +
                                                       metrics.TotalTimeViolations * 10000.0 +
                                                       metrics.AllFineOnSol * 2000.0,
            _ => metrics.TotalVehiclesUsed * 100.0 + metrics.TotalDistance * 10.0 +
                 metrics.TotalFuelCost * 3.5 + metrics.TotalTimeViolations * 3000.0 + metrics.AllFineOnSol * 1000.0
        };
    }

    // Турнирный отбор кандидатов для скрещивания 
    private Solution TournamentSelection()
    {
        if (_population == null || _population.Count == 0)
        {
            _logger?.LogWarning("TournamentSelection: пустая популяция, возвращаем дефолтное решение");
            return new Solution();
        }
        
        var tournament = new List<Solution>(_tournamentSize);
        for (int i = 0; i < _tournamentSize; i++)
        {
            tournament.Add(_population[R.Next(_population.Count)]);
        }

        return tournament.OrderBy(t => t.TotalFitness).First().Clone();
    }
    
    private List<Solution> Crossover()
    {
        var children = new Solution[_populationSize]; // Детерментрованная коллекция для Random
        
        // Генерируем детей параллельно
        Parallel.For(0, _populationSize, _parallelOptions, (int i) =>
        {
            var parent1 = TournamentSelection();
            var parent2 = TournamentSelection();

            if (R.NextDouble() > _crossoverRate)
            {
                children[i] = parent1.Clone(); // ← Присваиваем по индексу
                return;
            }

            var child = CreateChildFromParents(parent1, parent2);
    
            // Валидация
            if (ValidateTaskPairs(child) && 
                child.Routes.SelectMany(r => r.Genes.Select(g => g.TaskId)).Distinct().Count() == 
                GetUniqueTaskIds(parent1, parent2).Count())
            {
                children[i] = child.Clone(); // ← Присваиваем по индексу
            }
            else
            {
                // fallback: добавляем родителя, если ребёнок невалиден
                children[i] = parent1.Clone(); // ← Присваиваем по индексу
            }
        });

        return children.ToList();
    }


    private Solution CreateChildFromParents(Solution parent1, Solution parent2)
    {
        var child = new Solution();
        var taskPairTracker = new Dictionary<int, (bool HasLoad, bool HasUnload)>();

        // 🔹 Получаем ВСЕ задачи из обоих родителей
        var allTaskIds = GetUniqueTaskIds(parent1, parent2);

        // 1️⃣ Наследуем маршруты от родителей
        var allRoutes = new List<Chromosome>();
        allRoutes.AddRange(parent1.Routes.Where(r => r.Genes.Any()).Select(r => r.Clone()));
        allRoutes.AddRange(parent2.Routes.Where(r => r.Genes.Any()).Select(r => r.Clone()));

        var arr = allRoutes.ToArray();
        R.Shuffle(arr);
        allRoutes = arr.ToList();

        int routeToInherit = Math.Max(1, allRoutes.Count / 2);
        foreach (var route in allRoutes.Take(routeToInherit))
        {
            var genesToKeep = route.Genes
                .Where(g => !taskPairTracker.ContainsKey(g.TaskId))
                .Select(g => g.Clone()) // 🔹 Клонируем с сохранением AssignedFarmId
                .ToList();

            // Случайно пропускаем 33% задач для создания разнообразия
            if (genesToKeep.Count > 2 && R.NextDouble() < 0.5)
            {
                var genesToSkip = genesToKeep
                    .OrderBy(_ => R.Next())
                    .Take(genesToKeep.Count / 3) // Пропускаем 33%
                    .ToList();

                genesToKeep = genesToKeep.Except(genesToSkip).ToList();
            }

            if (genesToKeep.Any())
            {
                var newRoute = new Chromosome(route.VehicleId);
                newRoute.Genes.AddRange(genesToKeep);
                child.Routes.Add(newRoute);

                foreach (var gene in genesToKeep)
                {
                    taskPairTracker.TryAdd(gene.TaskId, (false, false));
                    var current = taskPairTracker[gene.TaskId];
                    if (gene.Operation == OperationType.Load)
                        taskPairTracker[gene.TaskId] = (true, current.HasUnload);
                    else
                        taskPairTracker[gene.TaskId] = (current.HasLoad, true);
                }
            }
        }

        // Ремонт - добавляем НЕДОСТАЮЩИЕ задачи
        foreach (var taskId in allTaskIds)
        {
            var status = taskPairTracker.GetValueOrDefault(taskId, (false, false));

            // Если задача уже полная - пропускаем
            if (status is { Item1: true, Item2: true })
                continue;

            var taskDto = GetTaskDto(parent1, parent2, taskId);
            if (taskDto == null)
            {
                _logger?.LogWarning("Crossover: не удалось найти DTO для задачи {TaskId}", taskId);
                continue;
            }

            // Наследуем AssignedFarmId от родителя с вероятностью 50%
            int? childFarmId = null;
            if (R.NextDouble() < 0.5)
            {
                var parentLoadGene = parent1.Routes.SelectMany(r => r.Genes)
                                           .FirstOrDefault(g =>
                                               g.TaskId == taskId && g.Operation == OperationType.Load)
                                       ?? parent2.Routes.SelectMany(r => r.Genes)
                                           .FirstOrDefault(g =>
                                               g.TaskId == taskId && g.Operation == OperationType.Load);

                childFarmId = parentLoadGene?.AssignedFarmId;
            }

            // Если не унаследовали или у родителя не было — случайная ферма
            if (!childFarmId.HasValue)
            {
                var allFarms = _decodingContext.GetFarmLocations()?.Values.Select(f => f.Index).ToList();
                if (allFarms?.Any() == true)
                {
                    childFarmId = allFarms[R.Next(allFarms.Count)];
                }
            }

            var loadOp = new Gene(taskId, OperationType.Load, taskDto, childFarmId);
            var unloadOp = new Gene(taskId, OperationType.Unload, taskDto, null);

            Chromosome targetRoute;
            var routeWithPartialTask = child.Routes
                .FirstOrDefault(r => r.Genes.Any(g => g.TaskId == taskId));

            if (routeWithPartialTask != null)
            {
                // Задача уже есть в этом маршруте - добавляем недостающую часть ТУДА ЖЕ
                targetRoute = routeWithPartialTask;
            }
            else
            {
                // Задачи нет ни в одном маршруте - выбираем куда добавить
                // 70% — добавить в существующий случайный маршрут, 30% — создать новый если есть свободные машины
                if (child.Routes.Any() && R.NextDouble() < 0.7)
                {
                    targetRoute = child.Routes[R.Next(child.Routes.Count)];
                }
                else
                {
                    targetRoute = GetTargetRouteForInsertion(child);
                    child.Routes.Add(targetRoute);
                }
            }



            // Добавляем операции в правильном порядке
            switch (status)
            {
                case { Item1: false, Item2: true }: // Есть только Unload
                {
                    var existingUnload = targetRoute.Genes.FirstOrDefault(g =>
                        g.TaskId == taskId && g.Operation == OperationType.Unload);

                    if (existingUnload != null)
                    {
                        int idx = targetRoute.Genes.IndexOf(existingUnload);
                        targetRoute.Genes.Insert(R.Next(0, idx), loadOp);
                    }
                    else
                    {
                        // Fallback: просто добавляем Load первым
                        targetRoute.Genes.Insert(0, loadOp);
                    }

                    break;
                }
                case { Item1: true, Item2: false }: // Есть только Load
                {
                    var existingLoad = targetRoute.Genes.FirstOrDefault(g =>
                        g.TaskId == taskId && g.Operation == OperationType.Load);

                    if (existingLoad != null)
                    {
                        int idx = targetRoute.Genes.IndexOf(existingLoad);
                        targetRoute.Genes.Insert(R.Next(idx + 1, targetRoute.Genes.Count + 1), unloadOp);
                    }
                    else
                    {
                        // Fallback: добавляем Unload в конец
                        targetRoute.Genes.Add(unloadOp);
                    }

                    break;
                }
                default: // Нет ничего - добавляем пару
                {
                    // Вставляем Load в случайную позицию
                    int loadPos = targetRoute.Genes.Count > 0
                        ? R.Next(0, targetRoute.Genes.Count + 1)
                        : 0;
                    targetRoute.Genes.Insert(loadPos, loadOp);

                    // Unload строго после Load
                    targetRoute.Genes.Insert(R.Next(loadPos + 1, targetRoute.Genes.Count + 1), unloadOp);
                    break;
                }
            }

            // Обновляем трекер
            taskPairTracker[taskId] = (true, true);
        }

        // // ФИНАЛЬНАЯ ПРОВЕРКА: гарантируем, что ВСЕ задачи присутствуют
        // var childTaskIds = child.Routes.SelectMany(r => r.Genes.Select(g => g.TaskId)).Distinct().ToList();
        // var missingTaskIds = allTaskIds.Where(id => !childTaskIds.Contains(id)).ToList();
        //
        // if (missingTaskIds.Any())
        // {
        //     _logger?.LogWarning("Crossover: отсутствуют задачи {MissingTasks} в ребёнке, добавляем принудительно",
        //         string.Join(", ", missingTaskIds));
        //     
        //     var newRoute = new Chromosome(
        //         child.Routes.Any() ? (child.Routes.Max(r => r.VehicleId) % 50) + 1 : 1);
        //     
        //     foreach (var taskId in missingTaskIds)
        //     {
        //         var taskDto = GetTaskDto(parent1, parent2, taskId);
        //         if (taskDto == null) continue;
        //
        //         // Создаём новый маршрут
        //         var posIdx = newRoute.Genes.Count > 0 ? R.Next(0, newRoute.Genes.Count + 1) : 0;
        //             
        //         newRoute.Genes.Insert(posIdx, new Gene(taskId, OperationType.Load, taskDto));
        //         newRoute.Genes.Insert(R.Next(posIdx + 1, newRoute.Genes.Count + 1), new Gene(taskId, OperationType.Unload, taskDto));
        //         
        //     }
        //     child.Routes.Add(newRoute);
        // }

        // Удаляем пустые маршруты
        child.Routes.RemoveAll(r => !r.Genes.Any());

        return child;
    }

    private List<int> GetUniqueTaskIds(Solution p1, Solution p2) => 
        p1.Routes.SelectMany(r => r.Genes)
            .Concat(p2.Routes.SelectMany(r => r.Genes))
            .Select(g => g.TaskId)
            .Distinct()
            .ToList();

    private DeliveryTaskDTO GetTaskDto(Solution p1, Solution p2, int taskId)
    {
        var gene = p1.Routes.SelectMany(r => r.Genes).FirstOrDefault(g => g.TaskId == taskId)
                   ?? p2.Routes.SelectMany(r => r.Genes).FirstOrDefault(g => g.TaskId == taskId);
        return gene?.Task;
    }

    private bool ValidateTaskPairs(Solution solution)
    {
        var taskCounts = solution.Routes
            .SelectMany(r => r.Genes)
            .GroupBy(g => g.TaskId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Каждая задача должна иметь ровно 2 операции (Load + Unload)
        return taskCounts.Values.All(count => count == 2);
    }

    private Solution Mutate(Solution solution)
    {
        // ВНУТРИМАРШРУТНАЯ МУТАЦИЯ (Swap & Reverse)
        foreach (var route in solution.Routes.Where(r => r.Genes.Any()).ToList())
        {
            // Swap: меняем местами два случайных гена
            if (R.NextDouble() <= _mutationRate && route.Genes.Count > 1)
            {
                int i1 = R.Next(route.Genes.Count);
                int i2 = R.Next(route.Genes.Count);
                (route.Genes[i1], route.Genes[i2]) = (route.Genes[i2], route.Genes[i1]);
            }

            // Reverse: разворачиваем случайный сегмент
            if (R.NextDouble() <= _mutationRate * 0.5 && route.Genes.Count > 2)
            {
                int start = R.Next(route.Genes.Count - 1);
                int end = R.Next(start + 1, route.Genes.Count);
                route.Genes.Reverse(start, end - start + 1);
            }
        }

        // МЕЖМАРШРУТНАЯ МУТАЦИЯ (перенос одной операции)
        if (R.NextDouble() <= _mutationRate * 0.8 && solution.Routes.Count > 1)
        {
            var srcRoute = solution.Routes
                .OrderBy(r => r.VehicleId)
                .FirstOrDefault(r => r.Genes.Any());
            if (srcRoute != null)
            {
                int idx = R.Next(srcRoute.Genes.Count);
                var geneToMove = srcRoute.Genes[idx];
                srcRoute.Genes.RemoveAt(idx);

                // Выбираем маршрут-приёмник (предпочтительно другой VehicleId)
                var dstRoute = solution.Routes
                    .OrderBy(r => r.VehicleId) 
                    .FirstOrDefault(r => r.VehicleId != srcRoute.VehicleId)
                               ?? solution.Routes[R.Next(solution.Routes.Count)];

                dstRoute.Genes.Insert(R.Next(dstRoute.Genes.Count + 1), geneToMove);
            }
        }
        
        // МУТАЦИЯ FARMID для Unload-операций
        var allFarms = _decodingContext.GetFarmLocations()?.Values.Select(f => f.Index).ToList();
    
        if (allFarms?.Any() == true)
        {
            foreach (var route in solution.Routes.Where(r => r.Genes.Any()).ToList())
            {
                foreach (var gene in route.Genes.Where(g => g.Operation == OperationType.Load).ToList())
                {
                    if (R.NextDouble() <= _farmMutationRate)
                    {
                        // Случайная новая ферма
                        gene.AssignedFarmId = allFarms[R.Next(allFarms.Count)];
                    }
                }
            }
        }

        // Смена VehicleId если есть свободные 
        if (R.NextDouble() <= _vehicleMutationRate)
        {
            var routeToChange = solution.Routes.FirstOrDefault(r => r.Genes.Any());
            if (routeToChange.Genes.Count == 0)
            {
                _logger.LogInformation("Нету генов в маршруте!");
            }
            else
            {
                var freeVehicle = GetFreeVehicleIds(solution);
                if (freeVehicle.Count != 0)
                {
                    routeToChange.VehicleId = freeVehicle[R.Next(freeVehicle.Count)];
                }
            }
        }

        // Проверяем и чиним пары Load/Unload (в т.ч. межмаршрутные)
        RepairSolutionPairs(solution);
        
        // Мутация-сжатие: иногда убираем часть маршрутов
        if (R.NextDouble() <= _mutationRate)
        {
            RedistributeAndRemoveRoutes(solution);

            // После перераспределения ещё раз страхуемся
            RepairSolutionPairs(solution);
        }

        // Удаляем пустые маршруты, образовавшиеся после мутаций
        solution.Routes.RemoveAll(r => !r.Genes.Any());
        
        return solution;
    }
    
    //Метод для урезания хромосом в решении чтобы они не увеличивались бесконечно 
    private void RedistributeAndRemoveRoutes(Solution solution)
    {
        var nonEmptyRoutes = solution.Routes
            .Where(r => r.Genes.Any())
            .ToList();

        if (nonEmptyRoutes.Count <= 1)
            return;

        int maxRoutesToRemove = nonEmptyRoutes.Count - 1;
        int routesToRemoveCount = R.Next(1, maxRoutesToRemove + 1);

        var shuffledRoutes = nonEmptyRoutes
            .OrderBy(_ => R.Next())
            .ToList();

        var routesToRemove = shuffledRoutes
            .Take(routesToRemoveCount)
            .ToList();

        var routesToKeep = shuffledRoutes
            .Skip(routesToRemoveCount)
            .ToList();

        if (!routesToKeep.Any())
        {
            var fallbackRoute = routesToRemove[^1];
            routesToRemove.RemoveAt(routesToRemove.Count - 1);
            routesToKeep.Add(fallbackRoute);
        }

        var pairsToRedistribute = new List<RouteTaskPair>();

        foreach (var route in routesToRemove)
        {
            pairsToRedistribute.AddRange(_pairMethod.ExtractTaskPairs(route));
            route.Genes.Clear();
        }

        if (!pairsToRedistribute.Any())
            return;

        // Делаем распределение неравномерным:
        // одна хромосома может получить заметно больше пар, другая меньше.
        // Создаём список с весами (детерминированный порядок)
        var routeWeights = routesToKeep
            .Select(route => (route, weight: 0.1 + R.NextDouble()))
            .ToList();

        foreach (var pair in pairsToRedistribute.OrderBy(_ => R.Next()))
        {
            // Выбираем маршрут с учётом весов
            var targetRoute = _pairMethod.PickWeightedRouteFromList(routeWeights, R);
            _pairMethod.InsertTaskPairIntoRoute(targetRoute, pair.Load, pair.Unload, R);
        }


        solution.Routes.RemoveAll(r => !r.Genes.Any());
    }

    // <summary>
    /// Восстанавливает целостность пар Load/Unload после мутаций.
    /// 1. Собирает разорванные пары из разных маршрутов в один.
    /// 2. Исправляет порядок: Load всегда идёт до Unload.
    /// 3. При исправлении порядка вставляет Load в случайную позицию ПЕРЕД Unload 
    ///    (сохраняет допустимость, но повышает генетическое разнообразие).
    /// </summary>
    private void RepairSolutionPairs(Solution solution)
    {
        // Собираем все уникальные TaskId, присутствующие в решении
        var taskIds = solution.Routes.SelectMany(r => r.Genes)
            .Select(g => g.TaskId)
            .Distinct()
            .ToList();

        foreach (int taskId in taskIds)
        {
            Chromosome loadRoute = null, unloadRoute = null;
            int loadIdx = -1, unloadIdx = -1;

            // 🔍 1. Находим, в каких маршрутах и на каких позициях находятся операции
            foreach (var route in solution.Routes)
            {
                if (loadIdx == -1)
                {
                    loadIdx = route.Genes.FindIndex(g => g.TaskId == taskId && g.Operation == OperationType.Load);
                    if (loadIdx != -1) loadRoute = route;
                }

                if (unloadIdx == -1)
                {
                    unloadIdx = route.Genes.FindIndex(g => g.TaskId == taskId && g.Operation == OperationType.Unload);
                    if (unloadIdx != -1) unloadRoute = route;
                }
            }
            
            if (loadRoute == null && unloadRoute == null)
                continue; 

            // Если одной из операций нет вообще — пропускаем (защита от багов данных)
            if (loadRoute == null && unloadRoute != null)
            {
                
                var taskDto = unloadRoute.Genes.First(g => g.TaskId == taskId).Task;
                
                // 🔹 Сохраняем или назначаем ферму
                int? farmId = null;
                var allFarms = _decodingContext.GetFarmLocations()?.Values.Select(f => f.Index).ToList();
                if (allFarms?.Any() == true)
                {
                    farmId = allFarms[R.Next(allFarms.Count)];
                }

                var loadGene = new Gene(taskId, OperationType.Load, taskDto, farmId ?? taskDto?.FarmId);
            
                // Вставляем Load строго ПЕРЕД существующим Unload
                int insertPos = R.Next(0, unloadIdx + 1);
                unloadRoute.Genes.Insert(insertPos, loadGene);
                continue;
            }
            
            // При создании недостающего Unload:
            if (loadRoute != null && unloadRoute == null)
            {
                var taskDto = loadRoute.Genes.First(g => g.TaskId == taskId).Task;
                
                var unloadGene = new Gene(taskId, OperationType.Unload, taskDto, null);
    
                // Вставляем после Load...
                int insertPos = R.Next(loadIdx + 1, loadRoute.Genes.Count + 1);
                loadRoute.Genes.Insert(insertPos, unloadGene);
                continue;
            }

            // 2. Исправление межмаршрутного разрыва
            if (loadRoute != unloadRoute)
            {
                // Переносим Unload в маршрут, где уже есть Load
                var unloadGene = unloadRoute.Genes[unloadIdx];
                unloadRoute.Genes.RemoveAt(unloadIdx);

                // Вставляем Unload в случайное место ПОСЛЕ Load в целевом маршруте
                // loadIdx всё ещё валиден, т.к. работаем с другим маршрутом
                int insertPos = R.Next(loadIdx + 1, loadRoute.Genes.Count + 1);
                loadRoute.Genes.Insert(insertPos, unloadGene);

                // Теперь обе операции в одном маршруте
                unloadRoute = loadRoute;
            }

            // 3. Исправление порядка внутри маршрута (если Load оказался после Unload)
            // Пересчитываем индексы на случай, если была межмаршрутная миграция
            loadIdx = unloadRoute.Genes.FindIndex(g => g.TaskId == taskId && g.Operation == OperationType.Load);
            unloadIdx = unloadRoute.Genes.FindIndex(g => g.TaskId == taskId && g.Operation == OperationType.Unload);

            if (loadIdx > unloadIdx) // Нарушение: Load идёт после Unload
            {
                var loadGene = unloadRoute.Genes[loadIdx];
                unloadRoute.Genes.RemoveAt(loadIdx);

                // Случайная позиция ПЕРЕД Unload (от 0 до unloadIdx включительно)
                // После RemoveAt индекс Unload сдвинулся на unloadIdx, 
                // поэтому Next(0, unloadIdx + 1) даёт все позиции строго до него
                int newInsertPos = R.Next(0, unloadIdx + 1);
                unloadRoute.Genes.Insert(newInsertPos, loadGene);
            }
        }
    }
    
    //Доп методы для работы ГА
    private List<int> GetActiveVehicleIds() =>
        _decodingContext.AvailableVehicles
            .Where(v => v.IsActive)
            .Select(v => v.Id)
            .Distinct()
            .ToList();

    private string HashSolution(Solution s)
    {
        return string.Join("|",
            s.Routes.Select(r =>
                $"{r.VehicleId}:" +
                string.Join(",", r.Genes.Select(g =>
                    $"{g.TaskId}-{g.Operation}-{g.AssignedFarmId}"))));
    }
    private List<int> GetFreeVehicleIds(Solution solution)
    {
        var usedVehicleIds = solution.Routes
            .Select(r => r.VehicleId)
            .ToHashSet();

        return GetActiveVehicleIds()
            .Where(id => !usedVehicleIds.Contains(id))
            .ToList();
    }

    private Chromosome GetOrCreateRouteForVehicle(Solution solution, int vehicleId)
    {
        var existingRoute = solution.Routes.FirstOrDefault(r => r.VehicleId == vehicleId);
        if (existingRoute != null)
            return existingRoute;

        var newRoute = new Chromosome(vehicleId);
        solution.Routes.Add(newRoute);
        return newRoute;
    }

    private Chromosome GetTargetRouteForInsertion(Solution solution)
    {
        var freeVehicleIds = GetFreeVehicleIds(solution);
        if (freeVehicleIds.Any())
        {
            var freeVehicleId = freeVehicleIds[R.Next(freeVehicleIds.Count)];
            return GetOrCreateRouteForVehicle(solution, freeVehicleId);
        }

        var activeVehicleIds = GetActiveVehicleIds();
        if (!activeVehicleIds.Any())
            throw new InvalidOperationException("Нет активных машин для построения маршрутов.");

        var vehicleId = activeVehicleIds[R.Next(activeVehicleIds.Count)];

        // Если свободных машин нет, здесь вернётся уже существующий маршрут
        return GetOrCreateRouteForVehicle(solution, vehicleId);
    }
    
    public void Dispose()
    {
        // Освобождаем ThreadLocal объекты
        _decoder?.Dispose();
        _threadRandom?.Dispose();
        
        // Очищаем коллекции
        _population?.Clear();
        _fitnessHistory?.Clear();
        _bestSolution = null;
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}