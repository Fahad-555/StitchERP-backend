using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StitchERP.Application.Programs;
using StitchERP.Infrastructure.Data;

namespace StitchERP.Infrastructure.Programs;

public sealed class MySqlProgramBomService(StitchErpDbContext db) : IProgramBomService
{
    public IReadOnlyCollection<ProgramSummary> GetPrograms()
    {
        using var command = Command("SELECT p.program_id, p.program_code, p.program_name, p.program_status, c.customer_name FROM programs p JOIN customers c ON c.customer_id = p.customer_id WHERE p.is_active = 1 ORDER BY p.program_id");
        using var reader = command.ExecuteReader();
        var result = new List<ProgramSummary>();
        while (reader.Read()) result.Add(new ProgramSummary(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return result;
    }

    public ProgramSummary CreateProgram(CreateProgramRequest request)
    {
        if (request.OrganizationId <= 0 || request.CustomerId <= 0 || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("OrganizationId, CustomerId, program code and name are required.");
        using var connection = NewConnection(); connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO programs (organization_id, customer_id, program_code, program_name, brand, program_status, is_active, version_no) VALUES (@org, @customer, @code, @name, @brand, 'DRAFT', 1, 1); SELECT LAST_INSERT_ID();";
        Add(command, ("@org", request.OrganizationId), ("@customer", request.CustomerId), ("@code", request.Code.Trim()), ("@name", request.Name.Trim()), ("@brand", (object?)request.Brand ?? DBNull.Value));
        var id = Convert.ToInt64(command.ExecuteScalar());
        return GetPrograms().First(x => x.Id == id);
    }

    public ProgramSummary SubmitProgram(long id)
    {
        using var connection = NewConnection(); connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE programs SET program_status = 'SUBMITTED', updated_at = CURRENT_TIMESTAMP WHERE program_id = @id AND program_status = 'DRAFT';"; Add(command, ("@id", id)); if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Only an existing draft program can be submitted."); return GetPrograms().First(x => x.Id == id);
    }

    public BomSummary CreateBom(CreateBomRequest request)
    {
        if (request.ProgramId <= 0 || request.ArticleId <= 0 || request.Lines.Count == 0) throw new ArgumentException("ProgramId, ArticleId and BOM lines are required.");
        using var connection = NewConnection(); connection.Open(); using var transaction = connection.BeginTransaction();
        using var header = connection.CreateCommand(); header.Transaction = transaction; header.CommandText = "INSERT INTO bom_headers (program_id, article_id, bom_version, bom_status, active_flag, created_by) SELECT @program, @article, COALESCE(MAX(bom_version), 0) + 1, 'DRAFT', 1, @user FROM bom_headers WHERE program_id = @program AND article_id = @article; SELECT LAST_INSERT_ID();"; Add(header, ("@program", request.ProgramId), ("@article", request.ArticleId), ("@user", 1L)); var id = Convert.ToInt64(header.ExecuteScalar());
        foreach (var line in request.Lines) { using var detail = connection.CreateCommand(); detail.Transaction = transaction; detail.CommandText = "INSERT INTO bom_lines (bom_id, material_type, material_reference_id, uom, consumption_qty, waste_pct, unit_rate) VALUES (@bom, @type, @reference, @uom, @quantity, @waste, @rate)"; Add(detail, ("@bom", id), ("@type", line.MaterialType), ("@reference", line.MaterialReferenceId), ("@uom", (object?)line.Uom ?? DBNull.Value), ("@quantity", line.ConsumptionQuantity), ("@waste", line.WastePercentage), ("@rate", (object?)line.UnitRate ?? DBNull.Value)); detail.ExecuteNonQuery(); }
        transaction.Commit(); return new BomSummary(id, request.ProgramId, request.ArticleId, 1, "DRAFT", request.Lines);
    }

    public BomSummary SubmitBom(long id) { using var command = Command("UPDATE bom_headers SET bom_status = 'SUBMITTED', updated_at = CURRENT_TIMESTAMP WHERE bom_id = @id AND bom_status = 'DRAFT'"); Add(command, ("@id", id)); if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Only an existing draft BOM can be submitted."); return new BomSummary(id, 0, 0, 0, "SUBMITTED", []); }
    private MySqlConnection NewConnection() => new(db.Database.GetConnectionString()!);
    private IDbCommand Command(string sql) { var connection = NewConnection(); connection.Open(); var command = connection.CreateCommand(); command.CommandText = sql; return command; }
    private static void Add(IDbCommand command, params (string Name, object Value)[] values) { foreach (var value in values) { var parameter = command.CreateParameter(); parameter.ParameterName = value.Name; parameter.Value = value.Value; command.Parameters.Add(parameter); } }
}
