using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    [ApiController]
    [Route("api/tariffs")]
    [AllowAnonymous]
    public class TariffsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public TariffsController(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class TariffUpload
        {
            public string FuelType { get; set; } = "dual";   // "dual" or "elec"
            public int RegionId { get; set; }
            public string RegionName { get; set; }
            public string Gsp { get; set; }
            public int DistributorId { get; set; }
            public string Postcode { get; set; }
            public DateTime FetchedAt { get; set; }
            public decimal ElecKwh { get; set; }
            public decimal GasKwh { get; set; }
            public long QuoteId { get; set; }
            public List<TariffUploadRow> Tariffs { get; set; } = new();
        }

        public class TariffUploadRow
        {
            public int SupplierId { get; set; }
            public string SupplierCode { get; set; }
            public string SupplierName { get; set; }
            public string TariffName { get; set; }
            public string TariffCode { get; set; }
            public string TariffTypes { get; set; }
            public string PaymentType { get; set; }
            public int? FixedTermMonths { get; set; }
            public string FixedTermEndDate { get; set; }
            public decimal AnnualCost { get; set; }
            public decimal Saving { get; set; }
            public decimal ElecCost { get; set; }
            public decimal GasCost { get; set; }
            public decimal ElecUnitRate { get; set; }
            public decimal ElecStandingCharge { get; set; }
            public decimal GasUnitRate { get; set; }
            public decimal GasStandingCharge { get; set; }
            public decimal? ElecExitFee { get; set; }
            public decimal? GasExitFee { get; set; }
            public decimal? MonthlyFee { get; set; }
            public bool PaperlessBills { get; set; }
            public bool Switchable { get; set; }
            public string SignupUrl { get; set; }
        }

        private class RegionRow { public int RegionId { get; set; } public string Name { get; set; } public string Gsp { get; set; } public string Postcode { get; set; } public int? DistributorId { get; set; } public decimal? ElecKwh { get; set; } public decimal? GasKwh { get; set; } public DateTime? FetchedAt { get; set; } public int TariffCount { get; set; } public int? ElecTariffCount { get; set; } public DateTime? ElecFetchedAt { get; set; } public decimal? ElecOnlyKwh { get; set; } public int? GasTariffCount { get; set; } public DateTime? GasFetchedAt { get; set; } public decimal? GasOnlyKwh { get; set; } }
        private const string RegionCols = "RegionId, Name, Gsp, Postcode, DistributorId, ElecKwh, GasKwh, FetchedAt, TariffCount, ElecTariffCount, ElecFetchedAt, ElecOnlyKwh, GasTariffCount, GasFetchedAt, GasOnlyKwh";
        private static string NormFuel(string f) =>
            string.Equals(f, "elec", StringComparison.OrdinalIgnoreCase) ? "elec" :
            string.Equals(f, "gas", StringComparison.OrdinalIgnoreCase) ? "gas" : "dual";
        private class TariffRow { public int Id { get; set; } public int RegionId { get; set; } public int? SupplierId { get; set; } public string SupplierCode { get; set; } public string SupplierName { get; set; } public string TariffName { get; set; } public string TariffCode { get; set; } public string TariffTypes { get; set; } public string PaymentType { get; set; } public int? FixedTermMonths { get; set; } public string FixedTermEndDate { get; set; } public decimal AnnualCost { get; set; } public decimal? Saving { get; set; } public decimal? ElecCost { get; set; } public decimal? GasCost { get; set; } public decimal? ElecUnitRate { get; set; } public decimal? ElecStandingCharge { get; set; } public decimal? GasUnitRate { get; set; } public decimal? GasStandingCharge { get; set; } public decimal? ElecExitFee { get; set; } public decimal? GasExitFee { get; set; } public decimal? MonthlyFee { get; set; } public bool PaperlessBills { get; set; } public bool Switchable { get; set; } public string SignupUrl { get; set; } public DateTime FetchedAt { get; set; } }
        private class OutwardRow { public string Outward { get; set; } public int RegionId { get; set; } public string Source { get; set; } }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromBody] TariffUpload up)
        {
            var key = _config["Tariffs:UploadKey"];
            if (string.IsNullOrEmpty(key) || Request.Headers["X-Tariff-Key"] != key)
                return Unauthorized(new { ok = false, error = "Bad upload key." });
            if (up == null || up.RegionId < 10 || up.RegionId > 23)
                return BadRequest(new { ok = false, error = "RegionId must be 10-23." });
            if (up.Tariffs == null || up.Tariffs.Count == 0)
                return BadRequest(new { ok = false, error = "No tariffs in upload." });

            // EnergyLinx's own region for the postcode wins over the region the scraper was asked for
            // (e.g. York postcodes are on the North East network, not Yorkshire).
            var reported = PostcodeRegionMap.RegionForGsp(up.Gsp);
            if (reported != null && reported != up.RegionId)
                up.RegionId = reported.Value;

            var fetched = up.FetchedAt == default ? DateTime.UtcNow : up.FetchedAt.ToUniversalTime();
            var name = PostcodeRegionMap.Regions[up.RegionId].Name;
            var fuel = NormFuel(up.FuelType);

            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Tariffs WHERE RegionId = {up.RegionId} AND FuelType = {fuel}");
            foreach (var t in up.Tariffs)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO dbo.Tariffs
(RegionId, FuelType, SupplierId, SupplierCode, SupplierName, TariffName, TariffCode, TariffTypes, PaymentType, FixedTermMonths, FixedTermEndDate, AnnualCost, Saving, ElecCost, GasCost, ElecUnitRate, ElecStandingCharge, GasUnitRate, GasStandingCharge, ElecExitFee, GasExitFee, MonthlyFee, PaperlessBills, Switchable, SignupUrl, FetchedAt)
VALUES ({up.RegionId}, {fuel}, {t.SupplierId}, {t.SupplierCode}, {t.SupplierName}, {t.TariffName}, {t.TariffCode}, {t.TariffTypes}, {t.PaymentType}, {t.FixedTermMonths}, {t.FixedTermEndDate}, {t.AnnualCost}, {t.Saving}, {t.ElecCost}, {t.GasCost}, {t.ElecUnitRate}, {t.ElecStandingCharge}, {t.GasUnitRate}, {t.GasStandingCharge}, {t.ElecExitFee}, {t.GasExitFee}, {t.MonthlyFee}, {t.PaperlessBills}, {t.Switchable}, {t.SignupUrl}, {fetched})");
            }

            if (fuel == "dual")
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
IF EXISTS (SELECT 1 FROM dbo.TariffRegions WHERE RegionId = {up.RegionId})
  UPDATE dbo.TariffRegions SET Name = {name}, Gsp = {up.Gsp}, Postcode = {up.Postcode}, DistributorId = {up.DistributorId}, ElecKwh = {up.ElecKwh}, GasKwh = {up.GasKwh}, QuoteId = {up.QuoteId}, FetchedAt = {fetched}, TariffCount = {up.Tariffs.Count} WHERE RegionId = {up.RegionId}
ELSE
  INSERT INTO dbo.TariffRegions (RegionId, Name, Gsp, Postcode, DistributorId, ElecKwh, GasKwh, QuoteId, FetchedAt, TariffCount) VALUES ({up.RegionId}, {name}, {up.Gsp}, {up.Postcode}, {up.DistributorId}, {up.ElecKwh}, {up.GasKwh}, {up.QuoteId}, {fetched}, {up.Tariffs.Count})");
            else if (fuel == "elec")
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
IF EXISTS (SELECT 1 FROM dbo.TariffRegions WHERE RegionId = {up.RegionId})
  UPDATE dbo.TariffRegions SET Name = {name}, Gsp = ISNULL(Gsp, {up.Gsp}), Postcode = ISNULL(Postcode, {up.Postcode}), ElecTariffCount = {up.Tariffs.Count}, ElecFetchedAt = {fetched}, ElecOnlyKwh = {up.ElecKwh} WHERE RegionId = {up.RegionId}
ELSE
  INSERT INTO dbo.TariffRegions (RegionId, Name, Gsp, Postcode, DistributorId, ElecKwh, GasKwh, QuoteId, FetchedAt, TariffCount, ElecTariffCount, ElecFetchedAt, ElecOnlyKwh) VALUES ({up.RegionId}, {name}, {up.Gsp}, {up.Postcode}, {up.DistributorId}, NULL, NULL, NULL, NULL, 0, {up.Tariffs.Count}, {fetched}, {up.ElecKwh})");
            else
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
IF EXISTS (SELECT 1 FROM dbo.TariffRegions WHERE RegionId = {up.RegionId})
  UPDATE dbo.TariffRegions SET Name = {name}, Gsp = ISNULL(Gsp, {up.Gsp}), Postcode = ISNULL(Postcode, {up.Postcode}), GasTariffCount = {up.Tariffs.Count}, GasFetchedAt = {fetched}, GasOnlyKwh = {up.GasKwh} WHERE RegionId = {up.RegionId}
ELSE
  INSERT INTO dbo.TariffRegions (RegionId, Name, Gsp, Postcode, DistributorId, ElecKwh, GasKwh, QuoteId, FetchedAt, TariffCount, GasTariffCount, GasFetchedAt, GasOnlyKwh) VALUES ({up.RegionId}, {name}, {up.Gsp}, {up.Postcode}, {up.DistributorId}, NULL, NULL, NULL, NULL, 0, {up.Tariffs.Count}, {fetched}, {up.GasKwh})");

            var outward = PostcodeRegionMap.Outward(up.Postcode);
            if (!string.IsNullOrEmpty(outward))
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
IF EXISTS (SELECT 1 FROM dbo.PostcodeRegions WHERE Outward = {outward})
  UPDATE dbo.PostcodeRegions SET RegionId = {up.RegionId}, Source = 'energylinx', UpdatedAt = SYSUTCDATETIME() WHERE Outward = {outward}
ELSE
  INSERT INTO dbo.PostcodeRegions (Outward, RegionId, Source) VALUES ({outward}, {up.RegionId}, 'energylinx')");
            }
            await tx.CommitAsync();

            return Ok(new { ok = true, regionId = up.RegionId, fuelType = fuel, tariffs = up.Tariffs.Count, fetchedAt = fetched });
        }

        [HttpGet("regions")]
        public async Task<IActionResult> Regions()
        {
            var rows = await _db.Database.SqlQuery<RegionRow>(FormattableStringFactory.Create($"SELECT {RegionCols} FROM dbo.TariffRegions")).ToListAsync();
            var learned = await _db.Database.SqlQuery<OutwardRow>($"SELECT Outward, RegionId, Source FROM dbo.PostcodeRegions").ToListAsync();
            var all = PostcodeRegionMap.Regions.Select(kv =>
            {
                var r = rows.FirstOrDefault(x => x.RegionId == kv.Key);
                return new
                {
                    regionId = kv.Key, gsp = kv.Value.Gsp, name = kv.Value.Name,
                    postcode = r?.Postcode, fetchedAt = r?.FetchedAt, tariffCount = r?.TariffCount ?? 0,
                    elecTariffCount = r?.ElecTariffCount ?? 0, elecFetchedAt = r?.ElecFetchedAt,
                    gasTariffCount = r?.GasTariffCount ?? 0, gasFetchedAt = r?.GasFetchedAt,
                    elecKwh = r?.ElecKwh, gasKwh = r?.GasKwh, reportedGsp = r?.Gsp,
                    learnedOutwards = learned.Where(l => l.RegionId == kv.Key).Select(l => l.Outward).OrderBy(o => o).ToList(),
                };
            }).OrderBy(x => x.regionId);
            return Ok(new { ok = true, regions = all });
        }

        // GET /api/tariffs?postcode=SK8 3JH&elecKwh=2900&gasKwh=12000[&fuel=elec]
        [HttpGet("")]
        public async Task<IActionResult> Get([FromQuery] string postcode, [FromQuery] decimal? elecKwh, [FromQuery] decimal? gasKwh, [FromQuery] string fuel)
        {
            var fuelType = NormFuel(fuel);
            var elecOnly = fuelType == "elec";
            var gasOnly = fuelType == "gas";
            var norm = PostcodeRegionMap.Normalise(postcode);
            if (norm.Length < 5)
                return BadRequest(new { ok = false, error = "A full UK postcode is required." });

            var outward = PostcodeRegionMap.Outward(norm);
            string source;
            int? regionId = (await _db.Database.SqlQuery<OutwardRow>($"SELECT Outward, RegionId, Source FROM dbo.PostcodeRegions WHERE Outward = {outward}").FirstOrDefaultAsync())?.RegionId;
            if (regionId != null) source = "energylinx";
            else { regionId = PostcodeRegionMap.RegionForArea(norm); source = "postcode-area"; }
            if (regionId == null)
                return NotFound(new { ok = false, error = $"No supply region known for postcode {postcode}." });

            var region = await _db.Database.SqlQuery<RegionRow>(FormattableStringFactory.Create($"SELECT {RegionCols} FROM dbo.TariffRegions WHERE RegionId = {{0}}", regionId)).FirstOrDefaultAsync();
            var meta = PostcodeRegionMap.Regions[regionId.Value];
            var count = elecOnly ? (region?.ElecTariffCount ?? 0) : gasOnly ? (region?.GasTariffCount ?? 0) : (region?.TariffCount ?? 0);
            if (region == null || count == 0)
                return Ok(new { ok = true, available = false, fuelType, regionId, regionName = meta.Name, gsp = meta.Gsp, regionSource = source,
                    error = elecOnly ? "No electricity-only tariff data has been collected for this region yet."
                          : gasOnly ? "No gas-only tariff data has been collected for this region yet."
                          : "No tariff data has been collected for this region yet." });

            var tariffs = await _db.Database.SqlQuery<TariffRow>($"SELECT Id, RegionId, SupplierId, SupplierCode, SupplierName, TariffName, TariffCode, TariffTypes, PaymentType, FixedTermMonths, FixedTermEndDate, AnnualCost, Saving, ElecCost, GasCost, ElecUnitRate, ElecStandingCharge, GasUnitRate, GasStandingCharge, ElecExitFee, GasExitFee, MonthlyFee, PaperlessBills, Switchable, SignupUrl, FetchedAt FROM dbo.Tariffs WHERE RegionId = {regionId} AND FuelType = {fuelType} ORDER BY AnnualCost").ToListAsync();

            bool personalised = (!gasOnly && elecKwh > 0) || (!elecOnly && gasKwh > 0);
            var e = gasOnly ? 0 : (elecKwh ?? (elecOnly ? region.ElecOnlyKwh : region.ElecKwh) ?? 0);
            var g = elecOnly ? 0 : (gasKwh ?? (gasOnly ? region.GasOnlyKwh : region.GasKwh) ?? 0);

            var list = tariffs.Select(t =>
            {
                decimal? est = null;
                if (personalised)
                {
                    var elec = gasOnly ? 0 : e * (t.ElecUnitRate ?? 0) / 100m + (t.ElecStandingCharge ?? 0) * 365m / 100m;
                    var gas = elecOnly ? 0 : g * (t.GasUnitRate ?? 0) / 100m + (t.GasStandingCharge ?? 0) * 365m / 100m;
                    est = Math.Round(elec + gas + (t.MonthlyFee ?? 0) * 12m, 2);
                }
                return new
                {
                    t.Id, t.SupplierId, t.SupplierCode, t.SupplierName, t.TariffName, t.TariffCode, t.TariffTypes, t.PaymentType,
                    t.FixedTermMonths, t.FixedTermEndDate, t.AnnualCost, t.Saving, t.ElecCost, t.GasCost,
                    t.ElecUnitRate, t.ElecStandingCharge, t.GasUnitRate, t.GasStandingCharge, t.ElecExitFee, t.GasExitFee, t.MonthlyFee,
                    t.PaperlessBills, t.Switchable, t.SignupUrl,
                    estimatedAnnualCost = est,
                };
            });
            if (personalised) list = list.OrderBy(t => t.estimatedAnnualCost);

            return Ok(new
            {
                ok = true, available = true, fuelType,
                regionId, regionName = region.Name, gsp = region.Gsp ?? meta.Gsp, regionSource = source,
                fetchedAt = elecOnly ? region.ElecFetchedAt : gasOnly ? region.GasFetchedAt : region.FetchedAt, samplePostcode = region.Postcode,
                assumedElecKwh = gasOnly ? null : elecOnly ? region.ElecOnlyKwh : region.ElecKwh,
                assumedGasKwh = elecOnly ? null : gasOnly ? region.GasOnlyKwh : region.GasKwh,
                usedElecKwh = personalised && !gasOnly ? e : (decimal?)null, usedGasKwh = personalised && !elecOnly ? g : (decimal?)null,
                tariffs = list,
            });
        }
    }
}
