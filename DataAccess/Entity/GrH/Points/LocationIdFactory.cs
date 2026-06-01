namespace DataAccess.Entity.GrH;

public class LocationIdFactory
{
    public static int FromFarm(int farmId) => farmId;
    public static int FromStore(int storeId) => 10000 + storeId;
}