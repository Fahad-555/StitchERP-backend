using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StitchERP.Application.Inventory;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Infrastructure.Inventory;

public sealed class MySqlInventoryService(StitchErpDbContext db) : IInventoryService
{
    public IReadOnlyCollection<InventoryBalance> GetBalances()
    {
        using var command = Command("SELECT inventory_item_id, warehouse_id, item_reference_id, item_type, on_hand_qty, reserved_qty, allocated_qty FROM inventory_items");
        using var reader = command.ExecuteReader();
        var result = new List<InventoryBalance>();
        while (reader.Read()) result.Add(ReadBalance(reader));
        return result;
    }

    public InventoryTransactionResult PostStock(PostStockRequest request)
    {
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.TransactionType)) throw new ArgumentException("Stock quantity and transaction type are required.");
        using var connection = NewConnection(); connection.Open(); using var transaction = connection.BeginTransaction();
        var balance = Find(connection, transaction, request.WarehouseId, request.ItemReferenceId, request.ItemType);
        var inbound = request.TransactionType is "RECEIPT" or "ADJUSTMENT_IN";
        if (balance is null)
        {
            if (!inbound) throw new InvalidOperationException("An outbound transaction requires an existing inventory item.");
            Execute(connection, transaction, "INSERT INTO inventory_items (warehouse_id, item_reference_id, item_type, on_hand_qty, reserved_qty, allocated_qty) VALUES (@warehouse, @reference, @type, 0, 0, 0)", ("@warehouse", request.WarehouseId), ("@reference", request.ItemReferenceId), ("@type", request.ItemType));
            balance = Find(connection, transaction, request.WarehouseId, request.ItemReferenceId, request.ItemType)!;
        }
        var onHand = inbound ? balance.OnHandQuantity + request.Quantity : balance.OnHandQuantity - request.Quantity;
        if (onHand < balance.ReservedQuantity + balance.AllocatedQuantity) throw new InvalidOperationException("Stock cannot fall below reserved and allocated quantity.");
        Execute(connection, transaction, "UPDATE inventory_items SET on_hand_qty = @quantity, last_movement_at = CURRENT_TIMESTAMP WHERE inventory_item_id = @id", ("@quantity", onHand), ("@id", balance.Id));
        var transactionId = ExecuteScalar(connection, transaction, "INSERT INTO inventory_transactions (inventory_item_id, transaction_type, quantity, reference_type, reference_id, created_by) VALUES (@item, @type, @quantity, @referenceType, @referenceId, @createdBy); SELECT LAST_INSERT_ID();", ("@item", balance.Id), ("@type", request.TransactionType), ("@quantity", request.Quantity), ("@referenceType", (object?)request.ReferenceType ?? DBNull.Value), ("@referenceId", (object?)request.ReferenceId ?? DBNull.Value), ("@createdBy", request.CreatedBy));
        transaction.Commit();
        var updated = balance with { OnHandQuantity = onHand, AvailableQuantity = onHand - balance.ReservedQuantity - balance.AllocatedQuantity };
        return new InventoryTransactionResult(transactionId, updated.Id, request.Quantity, request.TransactionType, updated);
    }

    public InventoryTransactionResult Reserve(ReserveStockRequest request) => ChangeReservation(request, request.Quantity, "RESERVE");
    public InventoryTransactionResult Release(ReserveStockRequest request) => ChangeReservation(request, -request.Quantity, "RELEASE");

    private InventoryTransactionResult ChangeReservation(ReserveStockRequest request, decimal delta, string type)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Reservation quantity must be greater than zero.");
        using var connection = NewConnection(); connection.Open(); using var transaction = connection.BeginTransaction();
        var balance = Find(connection, transaction, request.InventoryItemId) ?? throw new KeyNotFoundException("Inventory item was not found.");
        var reserved = balance.ReservedQuantity + delta;
        if (reserved < 0 || reserved > balance.OnHandQuantity - balance.AllocatedQuantity) throw new InvalidOperationException("Reservation quantity is outside the available stock range.");
        Execute(connection, transaction, "UPDATE inventory_items SET reserved_qty = @reserved WHERE inventory_item_id = @id", ("@reserved", reserved), ("@id", balance.Id));
        var transactionId = ExecuteScalar(connection, transaction, "INSERT INTO inventory_transactions (inventory_item_id, transaction_type, quantity, created_by) VALUES (@item, @type, @quantity, @createdBy); SELECT LAST_INSERT_ID();", ("@item", balance.Id), ("@type", type), ("@quantity", request.Quantity), ("@createdBy", request.CreatedBy));
        transaction.Commit();
        var updated = balance with { ReservedQuantity = reserved, AvailableQuantity = balance.OnHandQuantity - reserved - balance.AllocatedQuantity };
        return new InventoryTransactionResult(transactionId, updated.Id, request.Quantity, type, updated);
    }

    private InventoryBalance? Find(IDbConnection connection, IDbTransaction transaction, long warehouseId, long itemReferenceId, string itemType) => QueryBalance(connection, transaction, "WHERE warehouse_id = @warehouse AND item_reference_id = @reference AND item_type = @type", ("@warehouse", warehouseId), ("@reference", itemReferenceId), ("@type", itemType));
    private InventoryBalance? Find(IDbConnection connection, IDbTransaction transaction, long id) => QueryBalance(connection, transaction, "WHERE inventory_item_id = @id", ("@id", id));
    private InventoryBalance? QueryBalance(IDbConnection connection, IDbTransaction transaction, string where, params (string Name, object Value)[] parameters) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"SELECT inventory_item_id, warehouse_id, item_reference_id, item_type, on_hand_qty, reserved_qty, allocated_qty FROM inventory_items {where} FOR UPDATE"; Add(command, parameters); using var reader = command.ExecuteReader(); return reader.Read() ? ReadBalance(reader) : null; }
    private MySqlConnection NewConnection() => new(db.Database.GetConnectionString()!);
    private IDbCommand Command(string sql) { var connection = NewConnection(); connection.Open(); var command = connection.CreateCommand(); command.CommandText = sql; return command; }
    private static void Execute(IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] parameters) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; Add(command, parameters); command.ExecuteNonQuery(); }
    private static long ExecuteScalar(IDbConnection connection, IDbTransaction transaction, string sql, params (string Name, object Value)[] parameters) { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; Add(command, parameters); return Convert.ToInt64(command.ExecuteScalar()); }
    private static void Add(IDbCommand command, params (string Name, object Value)[] parameters) { foreach (var parameter in parameters) { var item = command.CreateParameter(); item.ParameterName = parameter.Name; item.Value = parameter.Value; command.Parameters.Add(item); } }
    private static InventoryBalance ReadBalance(IDataRecord reader) { var onHand = reader.GetDecimal(4); var reserved = reader.GetDecimal(5); var allocated = reader.GetDecimal(6); return new InventoryBalance(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3), onHand, reserved, allocated, onHand - reserved - allocated); }
}
