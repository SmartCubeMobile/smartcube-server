<#
.SYNOPSIS
  Write the database STRUCTURE (tables, keys, defaults, indexes) as a SQL script. No data is exported.

.EXAMPLE
  .\Export-Schema.ps1                                   # production DB on this PC -> Migrations\000-baseline.sql
  .\Export-Schema.ps1 -Server "(localdb)\MSSQLLocalDB" -Database SmartCubeDev -Out schema-check.sql
#>
param(
    [string]$Server = '.',
    [string]$Database = 'netusers-SmartCubeMobile',
    [string]$Out = (Join-Path $PSScriptRoot 'Migrations\000-baseline.sql')
)

$ErrorActionPreference = 'Stop'
$cn = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$cn.Open()

function Q($sql) {
    $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql
    $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); return ,$t
}
function Br($n) { '[' + $n.Replace(']', ']]') + ']' }

$tables = Q "SELECT t.object_id, s.name AS sch, t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0 AND t.name <> 'SchemaVersions' ORDER BY t.name"
$cols = Q @"
SELECT c.object_id, c.column_id, c.name, ty.name AS type, c.max_length, c.precision, c.scale, c.is_nullable,
       c.is_identity, CAST(ic.seed_value AS bigint) AS seed, CAST(ic.increment_value AS bigint) AS incr,
       dc.name AS dname, dc.definition AS ddef, cc.definition AS computed
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
ORDER BY c.object_id, c.column_id
"@
$idx = Q @"
SELECT i.object_id, i.index_id, i.name, i.is_primary_key, i.is_unique_constraint, i.is_unique, i.type_desc, i.filter_definition,
       ic.key_ordinal, ic.is_descending_key, ic.is_included_column, c.name AS col
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.type > 0 AND OBJECTPROPERTY(i.object_id, 'IsMsShipped') = 0
ORDER BY i.object_id, i.index_id, ic.is_included_column, ic.key_ordinal, ic.index_column_id
"@
$fks = Q @"
SELECT fk.name, fk.parent_object_id, fk.referenced_object_id, OBJECT_NAME(fk.referenced_object_id) AS reftable,
       fk.delete_referential_action_desc AS ondelete, pc.name AS col, rc.name AS refcol, fkc.constraint_column_id
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY fk.name, fkc.constraint_column_id
"@
$cn.Close()

function TypeText($r) {
    $t = $r.type
    switch ($t) {
        { $_ -in 'nvarchar', 'nchar' } { if ($r.max_length -eq -1) { return "$t(max)" } return "$t($([int]$r.max_length / 2))" }
        { $_ -in 'varchar', 'char', 'varbinary', 'binary' } { if ($r.max_length -eq -1) { return "$t(max)" } return "$t($($r.max_length))" }
        { $_ -in 'decimal', 'numeric' } { return "$t($($r.precision),$($r.scale))" }
        { $_ -in 'datetime2', 'datetimeoffset', 'time' } { return "$t($($r.scale))" }
        default { return $t }
    }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- SmartCube database structure (no data). Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm') from $Database.")
[void]$sb.AppendLine("-- Safe to run on an empty database; tables that already exist are skipped.")
[void]$sb.AppendLine()

foreach ($t in $tables) {
    $full = "$(Br $t.sch).$(Br $t.name)"
    $lines = @()
    foreach ($c in ($cols | Where-Object { $_.object_id -eq $t.object_id })) {
        if ($c.computed -isnot [DBNull]) { $lines += "    $(Br $c.name) AS $($c.computed)"; continue }
        $l = "    $(Br $c.name) $(TypeText $c)"
        if ($c.is_identity) { $l += " IDENTITY($($c.seed),$($c.incr))" }
        $l += $(if ($c.is_nullable) { ' NULL' } else { ' NOT NULL' })
        if ($c.ddef -isnot [DBNull]) { $l += " CONSTRAINT $(Br $c.dname) DEFAULT $($c.ddef)" }
        $lines += $l
    }
    foreach ($g in ($idx | Where-Object { $_.object_id -eq $t.object_id -and ($_.is_primary_key -or $_.is_unique_constraint) } | Group-Object index_id)) {
        $i = $g.Group[0]
        $kind = if ($i.is_primary_key) { 'PRIMARY KEY' } else { 'UNIQUE' }
        $cl = if ($i.type_desc -eq 'CLUSTERED') { ' CLUSTERED' } else { ' NONCLUSTERED' }
        $keys = ($g.Group | ForEach-Object { (Br $_.col) + $(if ($_.is_descending_key) { ' DESC' } else { '' }) }) -join ', '
        $lines += "    CONSTRAINT $(Br $i.name) $kind$cl ($keys)"
    }
    [void]$sb.AppendLine("IF OBJECT_ID(N'$($full.Replace("'", "''"))', N'U') IS NULL")
    [void]$sb.AppendLine("CREATE TABLE $full (")
    [void]$sb.AppendLine(($lines -join ",`r`n"))
    [void]$sb.AppendLine(");")
    [void]$sb.AppendLine("GO")
}

[void]$sb.AppendLine()
foreach ($g in ($idx | Where-Object { -not $_.is_primary_key -and -not $_.is_unique_constraint } | Group-Object object_id, index_id | Sort-Object { $_.Group[0].name })) {
    $i = $g.Group[0]
    $t = $tables | Where-Object { $_.object_id -eq $i.object_id } | Select-Object -First 1
    if (-not $t) { continue }
    $full = "$(Br $t.sch).$(Br $t.name)"
    $keys = ($g.Group | Where-Object { -not $_.is_included_column } | ForEach-Object { (Br $_.col) + $(if ($_.is_descending_key) { ' DESC' } else { '' }) }) -join ', '
    $inc = ($g.Group | Where-Object { $_.is_included_column } | ForEach-Object { Br $_.col }) -join ', '
    $sql = "CREATE $(if ($i.is_unique) { 'UNIQUE ' })$(if ($i.type_desc -eq 'CLUSTERED') { 'CLUSTERED' } else { 'NONCLUSTERED' }) INDEX $(Br $i.name) ON $full ($keys)"
    if ($inc) { $sql += " INCLUDE ($inc)" }
    if ($i.filter_definition -isnot [DBNull]) { $sql += " WHERE $($i.filter_definition)" }
    [void]$sb.AppendLine("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'$($i.name.Replace("'", "''"))' AND object_id = OBJECT_ID(N'$($full.Replace("'", "''"))'))")
    [void]$sb.AppendLine("    $sql;")
    [void]$sb.AppendLine("GO")
}

[void]$sb.AppendLine()
foreach ($g in ($fks | Group-Object name)) {
    $f = $g.Group[0]
    $t = $tables | Where-Object { $_.object_id -eq $f.parent_object_id } | Select-Object -First 1
    $full = "$(Br $t.sch).$(Br $t.name)"
    $cols1 = ($g.Group | ForEach-Object { Br $_.col }) -join ', '
    $cols2 = ($g.Group | ForEach-Object { Br $_.refcol }) -join ', '
    $del = if ($f.ondelete -ne 'NO_ACTION') { " ON DELETE $($f.ondelete.Replace('_', ' '))" } else { '' }
    [void]$sb.AppendLine("IF OBJECT_ID(N'$($f.name.Replace("'", "''"))', N'F') IS NULL")
    [void]$sb.AppendLine("    ALTER TABLE $full ADD CONSTRAINT $(Br $f.name) FOREIGN KEY ($cols1) REFERENCES [dbo].$(Br $f.reftable) ($cols2)$del;")
    [void]$sb.AppendLine("GO")
}

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
[IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
"Wrote $Out ($($tables.Rows.Count) tables)"
