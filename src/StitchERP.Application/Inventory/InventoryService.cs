namespace StitchERP.Application.Inventory;

public sealed record InventoryBalance(long Id, long WarehouseId, long ItemReferenceId, string ItemType, decimal OnHandQuantity, decimal ReservedQuantity, decimal AllocatedQuantity, decimal AvailableQuantity);
public sealed record PostStockRequest(long WarehouseId, long ItemReferenceId, string ItemType, decimal Quantity, string TransactionType, string? ReferenceType, long? ReferenceId, long CreatedBy);
public sealed record ReserveStockRequest(long InventoryItemId, decimal Quantity, long CreatedBy);
public sealed record InventoryTransactionResult(long TransactionId, long InventoryItemId, decimal Quantity, string TransactionType, InventoryBalance Balance);

public interface IInventoryService
{
    IReadOnlyCollection<InventoryBalance> GetBalances();
    InventoryTransactionResult PostStock(PostStockRequest request);
    InventoryTransactionResult Reserve(ReserveStockRequest request);
    InventoryTransactionResult Release(ReserveStockRequest request);
}

public sealed class InventoryService : IInventoryService
{
    private readonly object sync = new();
    private readonly List<InventoryBalance> balances =
    [
        new(1, 1, 1, "ARTICLE", 500, 0, 0, 500)
    ];
    private long nextBalanceId = 1;
    private long nextTransactionId;

    public IReadOnlyCollection<InventoryBalance> GetBalances()
    {
        lock (sync) return balances.ToArray();
    }

    public InventoryTransactionResult PostStock(PostStockRequest request)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Stock quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.TransactionType)) throw new ArgumentException("TransactionType is required.");
        lock (sync)
        {
            var balance = balances.FirstOrDefault(x => x.WarehouseId == request.WarehouseId && x.ItemReferenceId == request.ItemReferenceId && x.ItemType == request.ItemType);
            if (balance is null)
            {
                if (request.TransactionType is not ("RECEIPT" or "ADJUSTMENT_IN")) throw new InvalidOperationException("An outbound transaction requires an existing inventory item.");
                balance = new InventoryBalance(++nextBalanceId, request.WarehouseId, request.ItemReferenceId, request.ItemType, 0, 0, 0, 0);
                balances.Add(balance);
            }
            var isInbound = request.TransactionType is "RECEIPT" or "ADJUSTMENT_IN";
            var newOnHand = isInbound ? balance.OnHandQuantity + request.Quantity : balance.OnHandQuantity - request.Quantity;
            if (newOnHand < balance.ReservedQuantity + balance.AllocatedQuantity)
                throw new InvalidOperationException("Stock cannot fall below reserved and allocated quantity.");
            balance = balance with { OnHandQuantity = newOnHand, AvailableQuantity = newOnHand - balance.ReservedQuantity - balance.AllocatedQuantity };
            Replace(balance);
            return Result(balance, request.Quantity, request.TransactionType);
        }
    }

    public InventoryTransactionResult Reserve(ReserveStockRequest request) => ChangeReservation(request, request.Quantity, "RESERVE");

    public InventoryTransactionResult Release(ReserveStockRequest request) => ChangeReservation(request, -request.Quantity, "RELEASE");

    private InventoryTransactionResult ChangeReservation(ReserveStockRequest request, decimal delta, string type)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Reservation quantity must be greater than zero.");
        lock (sync)
        {
            var balance = balances.FirstOrDefault(x => x.Id == request.InventoryItemId) ?? throw new KeyNotFoundException("Inventory item was not found.");
            var newReserved = balance.ReservedQuantity + delta;
            if (newReserved < 0) throw new InvalidOperationException("Reservation cannot be released below zero.");
            if (newReserved > balance.AvailableQuantity + balance.ReservedQuantity) throw new InvalidOperationException("Insufficient available stock for reservation.");
            balance = balance with { ReservedQuantity = newReserved, AvailableQuantity = balance.OnHandQuantity - newReserved - balance.AllocatedQuantity };
            Replace(balance);
            return Result(balance, request.Quantity, type);
        }
    }

    private void Replace(InventoryBalance balance)
    {
        var index = balances.FindIndex(x => x.Id == balance.Id);
        balances[index] = balance;
    }

    private InventoryTransactionResult Result(InventoryBalance balance, decimal quantity, string type)
        => new(++nextTransactionId, balance.Id, quantity, type, balance);
}
