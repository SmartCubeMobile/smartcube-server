<#
.SYNOPSIS
  Bring a database up to date by running every script in Migrations\ that it hasn't had yet.

  Database changes travel from development to production as numbered scripts:
      Migrations\000-baseline.sql, 001-add-something.sql, 002-...
  Each script runs once per database (recorded in dbo.SchemaVersions), in file-name order, inside a
  transaction. Never edit a script that has already been pulled into production: add a new one.

.EXAMPLE
  .\Apply-Migrations.ps1 -Server "(localdb)\MSSQLLocalDB" -Database SmartCubeDev -Create   # new dev DB
  .\Apply-Migrations.ps1                                                                    # production
  .\Apply-Migrations.ps1 -WhatIf                                                            # list only
#>
param(
    [string]$Server = '.',
    [string]$Database = 'netusers-SmartCubeMobile',
    [switch]$Create,          # create the database first if it doesn't exist (dev only)
    [switch]$MarkAsApplied,   # record pending scripts as applied WITHOUT running them (one-off for an existing DB)
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot 'Migrations'

function Open($db) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$db;Integrated Security=True;TrustServerCertificate=True"
    $c.Open(); return $c
}
function Exec($cn, $sql, $tx) {
    $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 300
    if ($tx) { $cmd.Transaction = $tx }
    [void]$cmd.ExecuteNonQuery()
}

if ($Create) {
    $m = Open 'master'
    $cmd = $m.CreateCommand(); $cmd.CommandText = "SELECT DB_ID(@n)"; [void]$cmd.Parameters.AddWithValue('@n', $Database)
    if ($cmd.ExecuteScalar() -is [DBNull]) {
        if ($WhatIf) { "Would create database $Database" } else { Exec $m "CREATE DATABASE [$($Database.Replace(']', ']]'))]"; "Created database $Database" }
    }
    $m.Close()
    if ($WhatIf) { return }
}

$cn = Open $Database
Exec $cn "IF OBJECT_ID('dbo.SchemaVersions') IS NULL CREATE TABLE dbo.SchemaVersions (ScriptName nvarchar(200) NOT NULL PRIMARY KEY, AppliedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), AppliedBy nvarchar(200) NULL)"
$cmd = $cn.CreateCommand(); $cmd.CommandText = "SELECT ScriptName FROM dbo.SchemaVersions"
$done = @{}; $r = $cmd.ExecuteReader(); while ($r.Read()) { $done[$r.GetString(0)] = $true }; $r.Close()

$pending = Get-ChildItem $dir -Filter '*.sql' | Sort-Object Name | Where-Object { -not $done.ContainsKey($_.Name) }
if (-not $pending) { "Database $Database is up to date."; $cn.Close(); return }

foreach ($f in $pending) {
    if ($WhatIf) { "Pending: $($f.Name)"; continue }
    $tx = $cn.BeginTransaction()
    try {
        if (-not $MarkAsApplied) {
            # Split on GO lines, like sqlcmd/SSMS do.
            $batches = [regex]::Split((Get-Content $f.FullName -Raw), '^\s*GO\s*$', 'Multiline') | Where-Object { $_.Trim() }
            foreach ($b in $batches) { Exec $cn $b $tx }
        }
        $ins = $cn.CreateCommand(); $ins.Transaction = $tx
        $ins.CommandText = "INSERT dbo.SchemaVersions (ScriptName, AppliedBy) VALUES (@n, @u)"
        [void]$ins.Parameters.AddWithValue('@n', $f.Name); [void]$ins.Parameters.AddWithValue('@u', "$env:USERDOMAIN\$env:USERNAME")
        [void]$ins.ExecuteNonQuery()
        $tx.Commit()
        "$(if ($MarkAsApplied) { 'Marked' } else { 'Applied' }): $($f.Name)"
    }
    catch {
        $tx.Rollback()
        $cn.Close()
        throw "Migration $($f.Name) failed and was rolled back: $($_.Exception.Message)"
    }
}
$cn.Close()
