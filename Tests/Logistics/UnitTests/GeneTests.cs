using BusinessLogic.SubServices.Logistics.Optimization;
using Xunit;

namespace Tests.UnitTests;

public class GeneTests : TestBase
{
    [Fact]
    public void Gene_Constructor_CreatesValidGene()
    {
        // Arrange
        var task = CreateTestTasks(1)[0];
        var taskId = 1;
        var operation = OperationType.Load;

        // Act
        var gene = new Gene(taskId, operation, task);

        // Assert
        Assert.Equal(taskId, gene.TaskId);
        Assert.Equal(operation, gene.Operation);
        Assert.Same(task, gene.Task);
    }

    [Fact]
    public void Gene_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var task = CreateTestTasks(1)[0];
        var original = new Gene(1, OperationType.Load, task);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.TaskId, clone.TaskId);
        Assert.Equal(original.Operation, clone.Operation);
        Assert.Same(original.Task, clone.Task); // Ссылка на тот же Task (иммутабельный)
        Assert.NotSame(original, clone);
    }
    
    [Fact]
    public void Constructor_WithAssignedFarmId_SetsProperty()
    {
        // Arrange
        var task = CreateTask(1, farmId: 10);
        
        // Act
        var gene = new Gene(1, OperationType.Unload, task, assignedFarmId: 5);
        
        // Assert
        Assert.Equal(5, gene.AssignedFarmId);
        Assert.Equal(5, gene.GetEffectiveFarmId());
    }

    [Fact]
    public void Constructor_WithoutAssignedFarmId_UsesTaskFarmId()
    {
        // Arrange
        var task = CreateTask(1, farmId: 10);
        
        // Act
        var gene = new Gene(1, OperationType.Unload, task, assignedFarmId: null);
        
        // Assert
        Assert.Null(gene.AssignedFarmId);
        Assert.Equal(10, gene.GetEffectiveFarmId());
    }

    [Fact]
    public void GetEffectiveFarmId_AssignedTakesPriority()
    {
        // Arrange
        var task = CreateTask(1, farmId: 10);
        var gene = new Gene(1, OperationType.Unload, task, assignedFarmId: 99);
        
        // Act & Assert
        Assert.Equal(99, gene.GetEffectiveFarmId());
    }

    [Fact]
    public void Clone_PreservesAssignedFarmId()
    {
        // Arrange
        var task = CreateTask(1, farmId: 10);
        var original = new Gene(1, OperationType.Unload, task, assignedFarmId: 7);
        
        // Act
        var clone = original.Clone();
        
        // Assert
        Assert.Equal(original.TaskId, clone.TaskId);
        Assert.Equal(original.Operation, clone.Operation);
        Assert.Equal(original.AssignedFarmId, clone.AssignedFarmId);
        Assert.Equal(original.GetEffectiveFarmId(), clone.GetEffectiveFarmId());
        Assert.NotSame(original, clone);
    }
}