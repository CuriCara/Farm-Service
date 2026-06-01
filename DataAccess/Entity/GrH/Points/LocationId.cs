namespace DataAccess.Entity.GrH;

public readonly struct LocationId(int value)
{
    public int Value { get; } = value;
}