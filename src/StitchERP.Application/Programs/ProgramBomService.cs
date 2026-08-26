namespace StitchERP.Application.Programs;

public sealed record ProgramSummary(long Id, string Code, string Name, string Status, string CustomerName);
public sealed record CreateProgramRequest(string Code, string Name, string CustomerName, string? Brand, long OrganizationId, long CustomerId);
public sealed record CreateBomRequest(long ProgramId, long ArticleId, IReadOnlyCollection<BomLineRequest> Lines);
public sealed record BomLineRequest(string MaterialType, long MaterialReferenceId, string? Uom, decimal ConsumptionQuantity, decimal WastePercentage, decimal? UnitRate);
public sealed record BomSummary(long Id, long ProgramId, long ArticleId, int Version, string Status, IReadOnlyCollection<BomLineRequest> Lines);

public interface IProgramBomService
{
    IReadOnlyCollection<ProgramSummary> GetPrograms();
    ProgramSummary CreateProgram(CreateProgramRequest request);
    ProgramSummary SubmitProgram(long id);
    BomSummary CreateBom(CreateBomRequest request);
    BomSummary SubmitBom(long id);
}

public sealed class ProgramBomService : IProgramBomService
{
    private readonly object sync = new();
    private readonly List<ProgramSummary> programs =
    [
        new(1, "PRG-001", "Spring Knit Program", "DRAFT", "Abbas & Co"),
        new(2, "PRG-002", "Summer Denim Program", "SUBMITTED", "North Street")
    ];
    private readonly List<BomSummary> boms = [];
    private long nextProgramId = 2;
    private long nextBomId;

    public IReadOnlyCollection<ProgramSummary> GetPrograms()
    {
        lock (sync) return programs.ToArray();
    }

    public ProgramSummary CreateProgram(CreateProgramRequest request)
    {
        if (request.OrganizationId <= 0 || request.CustomerId <= 0)
            throw new ArgumentException("OrganizationId and CustomerId must be positive.");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Program code and name are required.");

        lock (sync)
        {
            if (programs.Any(x => x.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A program with this code already exists.");
            var program = new ProgramSummary(++nextProgramId, request.Code.Trim(), request.Name.Trim(), "DRAFT", request.CustomerName.Trim());
            programs.Add(program);
            return program;
        }
    }

    public ProgramSummary SubmitProgram(long id)
    {
        lock (sync)
        {
            var index = programs.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("Program was not found.");
            var program = programs[index];
            if (program.Status != "DRAFT") throw new InvalidOperationException("Only draft programs can be submitted.");
            program = program with { Status = "SUBMITTED" };
            programs[index] = program;
            return program;
        }
    }

    public BomSummary CreateBom(CreateBomRequest request)
    {
        if (request.ProgramId <= 0 || request.ArticleId <= 0)
            throw new ArgumentException("ProgramId and ArticleId must be positive.");
        if (request.Lines.Count == 0) throw new ArgumentException("A BOM must contain at least one line.");
        if (request.Lines.Any(x => x.ConsumptionQuantity < 0 || x.WastePercentage < 0 || x.UnitRate < 0))
            throw new ArgumentException("BOM quantities, waste and rates cannot be negative.");

        lock (sync)
        {
            if (!programs.Any(x => x.Id == request.ProgramId)) throw new KeyNotFoundException("Program was not found.");
            var version = boms.Count(x => x.ProgramId == request.ProgramId && x.ArticleId == request.ArticleId) + 1;
            var bom = new BomSummary(++nextBomId, request.ProgramId, request.ArticleId, version, "DRAFT", request.Lines);
            boms.Add(bom);
            return bom;
        }
    }

    public BomSummary SubmitBom(long id)
    {
        lock (sync)
        {
            var index = boms.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("BOM was not found.");
            var bom = boms[index];
            if (bom.Status != "DRAFT") throw new InvalidOperationException("Only draft BOMs can be submitted.");
            bom = bom with { Status = "SUBMITTED" };
            boms[index] = bom;
            return bom;
        }
    }
}
