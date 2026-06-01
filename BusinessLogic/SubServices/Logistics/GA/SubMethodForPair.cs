using BusinessLogic.SubServices.Logistics.Optimization;
using DataAccess.Entity.Logistics.GA.Chromosome;

namespace BusinessLogic.SubServices.Logistics.GA;

public class SubMethodForPair
{
    // Метод для создания пар
    public List<RouteTaskPair> ExtractTaskPairs(Chromosome route)
    {
        return route.Genes
            .GroupBy(g => g.TaskId)
            .Select(group =>
            {
                var load = group.FirstOrDefault(g => g.Operation == OperationType.Load);
                var unload = group.FirstOrDefault(g => g.Operation == OperationType.Unload);

                return new { load, unload };
            })
            .Where(x => x.load != null && x.unload != null)
            .Select(x => new RouteTaskPair
            {
                Load = x.load!.Clone(),
                Unload = x.unload!.Clone()
            })
            .ToList();
    }
    
    // Метод для вставки пар
    public void InsertTaskPairIntoRoute(Chromosome route, Gene loadGene, Gene? unloadGene, Random random)
    {
        int loadPos = route.Genes.Count > 0
            ? random.Next(0, route.Genes.Count + 1)
            : 0;

        route.Genes.Insert(loadPos, loadGene);

        int unloadPos = random.Next(loadPos + 1, route.Genes.Count + 1);
        route.Genes.Insert(unloadPos, unloadGene);
    }
    
    // Метод для распределения веса хромосом для выборки
    public Chromosome PickWeightedRoute(IReadOnlyList<Chromosome> routes, Dictionary<Chromosome, double> weights)
    {
        if (routes.Count == 1)
            return routes[0];

        double totalWeight = routes.Sum(r => weights[r]);
        double roll = Random.Shared.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var route in routes)
        {
            cumulative += weights[route];
            if (roll <= cumulative)
                return route;
        }

        return routes[^1];
    }
    
    // Метод для распределения веса хромосом для выборки со списком (детерминированный)
    public Chromosome PickWeightedRouteFromList(List<(Chromosome route, double weight)>? routeWeights, Random R)
    {
        if (routeWeights.Count == 1)
            return routeWeights[0].route;

        double totalWeight = routeWeights.Sum(rw => rw.weight);
        double roll = R.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (route, weight) in routeWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return route;
        }

        return routeWeights[^1].route;
    }

}