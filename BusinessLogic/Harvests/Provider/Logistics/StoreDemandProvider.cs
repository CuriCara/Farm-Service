using DataAccess;
using DataAccess.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLogic.Logistics.Provider;

public class StoreDemandProvider
{
    private readonly FarmDbContext _db;

    public StoreDemandProvider(FarmDbContext db) => _db = db;

    public async Task<List<StoreDemand>> GetByDateAsync(DateOnly date) =>
        await _db.StoreDemands
            .Include(d => d.Store)
            .Include(d => d.Product)
            .Where(d => d.Date == date)
            .ToListAsync();

    public async Task<StoreDemand?> GetOneAsync(int storeId, int productId, DateOnly date) =>
        await _db.StoreDemands.FirstOrDefaultAsync(d =>
            d.StoreId == storeId &&
            d.ProductId == productId &&
            d.Date == date);

    public async Task SetRequiredQuantityAsync(int storeId, int productId, DateOnly date, double requiredQty)
    {
        var demand = await GetOneAsync(storeId, productId, date);

        if (demand == null)
        {
            demand = new StoreDemand
            {
                StoreId = storeId,
                ProductId = productId,
                Date = date,
                RequiredQuantity = requiredQty,
                PlannedQuantity = 0
            };
            await _db.StoreDemands.AddAsync(demand);
        }
        else
        {
            demand.RequiredQuantity = requiredQty;
            if (demand.PlannedQuantity > requiredQty)
            {
                demand.PlannedQuantity = requiredQty;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<StoreDemand>> GetUnfulfilledDemandsAsync(DateOnly date)
    {
        return await _db.StoreDemands
            .Include(d => d.Store)
            .Include(d => d.Product)
            .Where(d => d.Date == date && d.RequiredQuantity > d.PlannedQuantity)
            .ToListAsync();
    }

    public async Task UpdatePlannedQuantityAsync(int storeId, int productId, DateOnly date, double plannedQty)
    {
        var demand = await GetOneAsync(storeId, productId, date);
        if (demand == null)
            throw new InvalidOperationException("Требование не найдено");

        if (plannedQty > demand.RequiredQuantity)
        {
            throw new InvalidOperationException(
                $"Нельзя запланировать {plannedQty}, требуется только {demand.RequiredQuantity}");
        }

        demand.PlannedQuantity = plannedQty;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateFromDeliveryPlanAsync(DeliveryPlan plan)
    {
        foreach (var item in plan.Items)
        {
            var demand = await GetOneAsync(plan.StoreId, item.ProductId, plan.DeliveryDate);

            if (demand == null)
            {
                demand = new StoreDemand
                {
                    StoreId = plan.StoreId,
                    ProductId = item.ProductId,
                    Date = plan.DeliveryDate,
                    RequiredQuantity = item.Quantity,
                    PlannedQuantity = item.Quantity
                };
                await _db.StoreDemands.AddAsync(demand);
            }
            else
            {
                demand.PlannedQuantity = item.Quantity;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task ClearFromDeliveryPlanAsync(DeliveryPlan plan)
    {
        foreach (var item in plan.Items)
        {
            var demand = await GetOneAsync(plan.StoreId, item.ProductId, plan.DeliveryDate);
            if (demand != null)
            {
                demand.PlannedQuantity = 0;
            }
        }
        await _db.SaveChangesAsync();
    }
}