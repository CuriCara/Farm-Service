using BusinessLogic.SubServices.Logistics.Optimization;
using Xunit;

namespace Tests.UnitTests;

public class ChromosomeTests : TestBase
{
    [Fact]
    public void Chromosome_Constructor_SetsVehicleId()
    {
        // Act
        var chromosome = new Chromosome(5);

        // Assert
        Assert.Equal(5, chromosome.VehicleId);
        Assert.Empty(chromosome.Genes);
        Assert.Equal(0, chromosome.Fitness);
    }

    [Fact]
    public void Chromosome_Clone_CreatesDeepCopy()
    {
        // Arrange
        var tasks = CreateTestTasks(2);
        var original = new Chromosome(1);
        original.Genes.Add(new Gene(1, OperationType.Load, tasks[0]));
        original.Genes.Add(new Gene(1, OperationType.Unload, tasks[0]));
        original.Fitness = 42.5;
        original.Metrics.TotalDistance = 100;

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.VehicleId, clone.VehicleId);
        Assert.Equal(original.Fitness, clone.Fitness);
        Assert.Equal(original.Metrics.TotalDistance, clone.Metrics.TotalDistance);
        Assert.Equal(original.Genes.Count, clone.Genes.Count);
        
        // Гены должны быть клонированы, а не скопированы по ссылке
        for (int i = 0; i < original.Genes.Count; i++)
        {
            Assert.NotSame(original.Genes[i], clone.Genes[i]);
            Assert.Equal(original.Genes[i].TaskId, clone.Genes[i].TaskId);
            Assert.Equal(original.Genes[i].Operation, clone.Genes[i].Operation);
        }
        
        Assert.NotSame(original, clone);
    }
}