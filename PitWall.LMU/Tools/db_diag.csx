using System;
using DuckDB.NET.Data;

var conn = new DuckDBConnection(""Data Source=lmu_telemetry.db"");
conn.Open();
var cmd = conn.CreateCommand();

Console.WriteLine(""=== Lap Table (session 276) ==="");
cmd.CommandText = ""SELECT ts, value FROM \""Lap\"" WHERE session_id = 276 ORDER BY ts"";
using (var r = cmd.ExecuteReader()) {
    while(r.Read()) Console.WriteLine($""  ts={r.GetDouble(0):F3}  value={r.GetValue(1)}"");
}

Console.WriteLine(""\n=== GPS Time range ==="");
cmd.CommandText = ""SELECT COUNT(*), MIN(value), MAX(value) FROM \""GPS Time\"" WHERE session_id = 276"";
using (var r2 = cmd.ExecuteReader()) {
    r2.Read();
    Console.WriteLine($""  count={r2.GetValue(0)} min={r2.GetDouble(1):F3} max={r2.GetDouble(2):F3}"");
}

Console.WriteLine(""\n=== GPS Speed range ==="");
cmd.CommandText = ""SELECT COUNT(*), MIN(value), MAX(value) FROM \""GPS Speed\"" WHERE session_id = 276"";
using (var r3 = cmd.ExecuteReader()) {
    r3.Read();
    Console.WriteLine($""  count={r3.GetValue(0)} min={r3.GetDouble(1):F3} max={r3.GetDouble(2):F3}"");
}

Console.WriteLine(""\n=== First 5 GPS Time values ==="");
cmd.CommandText = ""SELECT ts, value FROM \""GPS Time\"" WHERE session_id = 276 ORDER BY ts LIMIT 5"";
using (var r4 = cmd.ExecuteReader()) {
    while(r4.Read()) Console.WriteLine($""  ts={r4.GetDouble(0):F3}  value={r4.GetDouble(1):F6}"");
}

Console.WriteLine(""\n=== Channel row counts ==="");
foreach (var t in new[] { ""GPS Time"", ""GPS Speed"", ""Throttle Pos"", ""Brake Pos"", ""Steering Pos"", ""Fuel Level"", ""TyresTempCentre"", ""Lap"" }) {
    cmd.CommandText = $""SELECT COUNT(*) FROM \""{t}\"" WHERE session_id = 276"";
    Console.WriteLine($""  {t}: {cmd.ExecuteScalar()}"");
}
