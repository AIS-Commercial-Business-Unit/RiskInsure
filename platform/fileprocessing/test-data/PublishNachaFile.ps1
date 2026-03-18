<#
.SYNOPSIS
    Generates a NACHA (ACH) file with randomly selected sample transactions.
.DESCRIPTION
    Produces a standards-compliant NACHA file with a File Header, one Batch,
    between 20 and 40 Entry Detail records drawn from 100 hardcoded sample
    accounts, and all required control records.
    Each record is exactly 94 characters. Amounts range from $100.00 to $1000.00.
.PARAMETER OutputPath
    Path to write the generated .ach file. Defaults to .\sample_<timestamp>.ach
.EXAMPLE
    .\PublishNachaFile.ps1
    .\PublishNachaFile.ps1 -OutputPath C:\temp\test.ach
#>
param(
    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function PadRight([string]$value, [int]$len) {
    if ($value.Length -ge $len) { return $value.Substring(0, $len) }
    return $value.PadRight($len)
}

function PadLeft([string]$value, [int]$len) {
    if ($value.Length -ge $len) { return $value.Substring(0, $len) }
    return $value.PadLeft($len, '0')
}

function Assert94([string]$record, [string]$label) {
    if ($record.Length -ne 94) {
        throw "Record '$label' is $($record.Length) chars, expected 94."
    }
}

# ---------------------------------------------------------------------------
# 100 Sample accounts — real routing numbers, fake account numbers & names
# Routing numbers verified via ABA check digit algorithm.
# ---------------------------------------------------------------------------
$SampleAccounts = @(
    # Chase Bank (021000021)
    @{ Routing="021000021"; Account="4520001001"; Name="JAMES ANDERSON" }
    @{ Routing="021000021"; Account="4520001002"; Name="MARIA GARCIA" }
    @{ Routing="021000021"; Account="4520001003"; Name="ROBERT JOHNSON" }
    @{ Routing="021000021"; Account="4520001004"; Name="LINDA MARTINEZ" }
    @{ Routing="021000021"; Account="4520001005"; Name="WILLIAM DAVIS" }
    # Bank of America (026009593)
    @{ Routing="026009593"; Account="8831002001"; Name="BARBARA WILSON" }
    @{ Routing="026009593"; Account="8831002002"; Name="RICHARD MOORE" }
    @{ Routing="026009593"; Account="8831002003"; Name="SUSAN TAYLOR" }
    @{ Routing="026009593"; Account="8831002004"; Name="JOSEPH JACKSON" }
    @{ Routing="026009593"; Account="8831002005"; Name="JESSICA WHITE" }
    # Wells Fargo (121042882)
    @{ Routing="121042882"; Account="6610003001"; Name="THOMAS HARRIS" }
    @{ Routing="121042882"; Account="6610003002"; Name="SARAH MARTIN" }
    @{ Routing="121042882"; Account="6610003003"; Name="CHARLES THOMPSON" }
    @{ Routing="121042882"; Account="6610003004"; Name="KAREN GARCIA" }
    @{ Routing="121042882"; Account="6610003005"; Name="CHRISTOPHER MARTINEZ" }
    # Citibank (021000089)
    @{ Routing="021000089"; Account="3340004001"; Name="PATRICIA ROBINSON" }
    @{ Routing="021000089"; Account="3340004002"; Name="DANIEL CLARK" }
    @{ Routing="021000089"; Account="3340004003"; Name="NANCY RODRIGUEZ" }
    @{ Routing="021000089"; Account="3340004004"; Name="MARK LEWIS" }
    @{ Routing="021000089"; Account="3340004005"; Name="BETTY LEE" }
    # US Bank (091000022)
    @{ Routing="091000022"; Account="7720005001"; Name="PAUL WALKER" }
    @{ Routing="091000022"; Account="7720005002"; Name="DOROTHY HALL" }
    @{ Routing="091000022"; Account="7720005003"; Name="GEORGE ALLEN" }
    @{ Routing="091000022"; Account="7720005004"; Name="SANDRA YOUNG" }
    @{ Routing="091000022"; Account="7720005005"; Name="KENNETH HERNANDEZ" }
    # PNC Bank (043000096)
    @{ Routing="043000096"; Account="5550006001"; Name="HELEN KING" }
    @{ Routing="043000096"; Account="5550006002"; Name="DONALD WRIGHT" }
    @{ Routing="043000096"; Account="5550006003"; Name="CAROL LOPEZ" }
    @{ Routing="043000096"; Account="5550006004"; Name="STEVEN HILL" }
    @{ Routing="043000096"; Account="5550006005"; Name="RUTH SCOTT" }
    # TD Bank (031101266)
    @{ Routing="031101266"; Account="2200007001"; Name="LARRY GREEN" }
    @{ Routing="031101266"; Account="2200007002"; Name="SHARON ADAMS" }
    @{ Routing="031101266"; Account="2200007003"; Name="EDWARD BAKER" }
    @{ Routing="031101266"; Account="2200007004"; Name="VIRGINIA GONZALEZ" }
    @{ Routing="031101266"; Account="2200007005"; Name="JASON NELSON" }
    # Capital One (056073502)
    @{ Routing="056073502"; Account="9980008001"; Name="DEBORAH CARTER" }
    @{ Routing="056073502"; Account="9980008002"; Name="RYAN MITCHELL" }
    @{ Routing="056073502"; Account="9980008003"; Name="KATHLEEN PEREZ" }
    @{ Routing="056073502"; Account="9980008004"; Name="GARY ROBERTS" }
    @{ Routing="056073502"; Account="9980008005"; Name="AMY TURNER" }
    # Truist Bank (061000104)
    @{ Routing="061000104"; Account="1110009001"; Name="PETER PHILLIPS" }
    @{ Routing="061000104"; Account="1110009002"; Name="ANGELA CAMPBELL" }
    @{ Routing="061000104"; Account="1110009003"; Name="HAROLD PARKER" }
    @{ Routing="061000104"; Account="1110009004"; Name="STEPHANIE EVANS" }
    @{ Routing="061000104"; Account="1110009005"; Name="WALTER EDWARDS" }
    # Fifth Third Bank (042000314)
    @{ Routing="042000314"; Account="4430010001"; Name="DIANA COLLINS" }
    @{ Routing="042000314"; Account="4430010002"; Name="JONATHAN STEWART" }
    @{ Routing="042000314"; Account="4430010003"; Name="BRENDA SANCHEZ" }
    @{ Routing="042000314"; Account="4430010004"; Name="RAYMOND MORRIS" }
    @{ Routing="042000314"; Account="4430010005"; Name="VIRGINIA ROGERS" }
    # KeyBank (041001039)
    @{ Routing="041001039"; Account="6670011001"; Name="SAMUEL REED" }
    @{ Routing="041001039"; Account="6670011002"; Name="WAYNE COOK" }
    @{ Routing="041001039"; Account="6670011003"; Name="THERESA MORGAN" }
    @{ Routing="041001039"; Account="6670011004"; Name="ARTHUR BELL" }
    @{ Routing="041001039"; Account="6670011005"; Name="JUDITH MURPHY" }
    # Regions Bank (062000019)
    @{ Routing="062000019"; Account="8890012001"; Name="FRED BAILEY" }
    @{ Routing="062000019"; Account="8890012002"; Name="VIRGINIA RIVERA" }
    @{ Routing="062000019"; Account="8890012003"; Name="GERALD COOPER" }
    @{ Routing="062000019"; Account="8890012004"; Name="AMANDA RICHARDSON" }
    @{ Routing="062000019"; Account="8890012005"; Name="ALAN COX" }
    # Huntington Bank (044000024)
    @{ Routing="044000024"; Account="3320013001"; Name="MILDRED HOWARD" }
    @{ Routing="044000024"; Account="3320013002"; Name="CARL WARD" }
    @{ Routing="044000024"; Account="3320013003"; Name="CHERYL TORRES" }
    @{ Routing="044000024"; Account="3320013004"; Name="BOBBY PETERSON" }
    @{ Routing="044000024"; Account="3320013005"; Name="VIRGINIA GRAY" }
    # Citizens Bank (011500010)
    @{ Routing="011500010"; Account="7750014001"; Name="PHILIP RAMIREZ" }
    @{ Routing="011500010"; Account="7750014002"; Name="DIANE JAMES" }
    @{ Routing="011500010"; Account="7750014003"; Name="RUSSELL WATSON" }
    @{ Routing="011500010"; Account="7750014004"; Name="SHIRLEY BROOKS" }
    @{ Routing="011500010"; Account="7750014005"; Name="RALPH KELLY" }
    # Ally Bank (124003116)
    @{ Routing="124003116"; Account="5560015001"; Name="JOAN SANDERS" }
    @{ Routing="124003116"; Account="5560015002"; Name="BRUCE PRICE" }
    @{ Routing="124003116"; Account="5560015003"; Name="EVELYN BENNETT" }
    @{ Routing="124003116"; Account="5560015004"; Name="ROY WOOD" }
    @{ Routing="124003116"; Account="5560015005"; Name="VIRGINIA BARNES" }
    # First Horizon (084000026)
    @{ Routing="084000026"; Account="2210016001"; Name="JACK ROSS" }
    @{ Routing="084000026"; Account="2210016002"; Name="JULIA HENDERSON" }
    @{ Routing="084000026"; Account="2210016003"; Name="TERRY COLEMAN" }
    @{ Routing="084000026"; Account="2210016004"; Name="MARILYN JENKINS" }
    @{ Routing="084000026"; Account="2210016005"; Name="DENNIS PERRY" }
    # BBVA (062001186)
    @{ Routing="062001186"; Account="9910017001"; Name="REBECCA POWELL" }
    @{ Routing="062001186"; Account="9910017002"; Name="SEAN LONG" }
    @{ Routing="062001186"; Account="9910017003"; Name="DORIS PATTERSON" }
    @{ Routing="062001186"; Account="9910017004"; Name="RAYMOND HUGHES" }
    @{ Routing="062001186"; Account="9910017005"; Name="VIRGINIA FLORES" }
    # East West Bank (322070381)
    @{ Routing="322070381"; Account="6640018001"; Name="HAROLD WASHINGTON" }
    @{ Routing="322070381"; Account="6640018002"; Name="GLORIA BUTLER" }
    @{ Routing="322070381"; Account="6640018003"; Name="EARL SIMMONS" }
    @{ Routing="322070381"; Account="6640018004"; Name="VIRGINIA FOSTER" }
    @{ Routing="322070381"; Account="6640018005"; Name="BOBBY GONZALES" }
    # Flagstar Bank (272471548)
    @{ Routing="272471548"; Account="4450019001"; Name="MILDRED BRYANT" }
    @{ Routing="272471548"; Account="4450019002"; Name="CLARENCE ALEXANDER" }
    @{ Routing="272471548"; Account="4450019003"; Name="VIRGINIA RUSSELL" }
    @{ Routing="272471548"; Account="4450019004"; Name="FRANK GRIFFIN" }
    @{ Routing="272471548"; Account="4450019005"; Name="LOUISE DIAZ" }
    # Glacier Bank (092905278)
    @{ Routing="092905278"; Account="7780020001"; Name="HENRY HAYES" }
    @{ Routing="092905278"; Account="7780020002"; Name="VIRGINIA MYERS" }
    @{ Routing="092905278"; Account="7780020003"; Name="RALPH FORD" }
    @{ Routing="092905278"; Account="7780020004"; Name="VIRGINIA HAMILTON" }
    @{ Routing="092905278"; Account="7780020005"; Name="RUSSELL GRAHAM" }
)

# ---------------------------------------------------------------------------
# NACHA configuration (originating company / bank details)
# ---------------------------------------------------------------------------
$ImmediateDestination  = " 021000021"   # leading space + 9-digit routing (Chase)
$ImmediateOrigin       = "1234567890"   # originator's 10-char ID
$ImmediateDestName     = PadRight "JPMORGAN CHASE"  23
$ImmediateOriginName   = PadRight "ACME CORP"       23
$CompanyName           = PadRight "ACME CORP"       16
$CompanyId             = "1234567890"   # 10-char
$CompanyEntryDesc      = PadRight "PAYROLL"  10
$ODFIRouting           = "02100002"     # first 8 digits of originating DFI (Chase)
$BatchNumber           = "0000001"

$TransactionCodes = @(
    "22",  # Checking credit (live)
    "22",  # Checking credit (live) — weighted higher
    "22",  # Checking credit (live) — weighted higher
    "27",  # Checking debit  (live)
    "32",  # Savings credit  (live)
    "37"   # Savings debit   (live)
)

# ---------------------------------------------------------------------------
# Generate file
# ---------------------------------------------------------------------------
$rng         = [System.Random]::new()
$entryCount  = $rng.Next(20, 41)   # 20..40 inclusive
$fileDate    = Get-Date -Format "yyMMdd"
$fileTime    = Get-Date -Format "HHmm"
$effectiveDate = (Get-Date).AddDays(1).ToString("yyMMdd")
$fileIdMod   = "A"

# Select entries (with replacement — same account can appear multiple times)
$entries = for ($i = 0; $i -lt $entryCount; $i++) {
    $acct   = $SampleAccounts[$rng.Next(0, $SampleAccounts.Count)]
    $cents  = $rng.Next(10000, 100001)   # $100.00 – $1000.00
    $txCode = $TransactionCodes[$rng.Next(0, $TransactionCodes.Count)]
    [pscustomobject]@{
        Routing     = $acct.Routing
        Account     = $acct.Account
        Name        = $acct.Name
        Cents       = $cents
        TxCode      = $txCode
        TraceSeq    = ($i + 1)
    }
}

# Pre-compute batch totals
$batchDebitTotal  = 0
$batchCreditTotal = 0
$batchEntryHash   = [long]0   # sum of first 8 digits of each routing number

foreach ($e in $entries) {
    if ($e.TxCode -in @("27","37")) { $batchDebitTotal  += $e.Cents }
    else                             { $batchCreditTotal += $e.Cents }
    $batchEntryHash += [long]($e.Routing.Substring(0,8))
}

# Entry hash is mod 10^10, zero-padded to 10
$batchEntryHashStr  = ($batchEntryHash % 10000000000L).ToString().PadLeft(10,'0')
$batchDebitStr      = $batchDebitTotal.ToString().PadLeft(12,'0')
$batchCreditStr     = $batchCreditTotal.ToString().PadLeft(12,'0')
$entryAddendaCount  = $entryCount   # no addenda records

# ---------------------------------------------------------------------------
# Build records
# ---------------------------------------------------------------------------
$records = [System.Collections.Generic.List[string]]::new()

# --- File Header (type 1) ---
# 1     Record type       1
# 2-3   Priority code     2   (always "01")
# 4-13  Immediate dest    10  (space + 9-digit routing)
# 14-23 Immediate origin  10
# 24-29 File creation date 6  YYMMDD
# 30-33 File creation time 4  HHMM
# 34    File ID modifier   1
# 35-37 Record size        3  (always "094")
# 38-39 Blocking factor    2  (always "10")
# 40-41 Format code        2  (always "1")
# 42-63 Immediate dest name 22
# 64-85 Immediate origin name 22
# 86-94 Reference code     9

$fileHeader = "1" +
              "01" +
              $ImmediateDestination +
              $ImmediateOrigin +
              $fileDate +
              $fileTime +
              $fileIdMod +
              "094" +
              "10" +
               "1" +
               $ImmediateDestName +
               $ImmediateOriginName +
               "        "
Assert94 $fileHeader "FileHeader"
$records.Add($fileHeader)

# --- Batch Header (type 5) ---
# 1     Record type       1
# 2-4   Service class     3   200=mixed, 220=credits, 225=debits
# 5-20  Company name      16
# 21-40 Company disc data 20
# 41-50 Company ID        10
# 51-53 SEC code           3  (PPD = prearranged payments)
# 54-63 Company entry desc 10
# 64-69 Company desc date  6  (spaces = current)
# 70-75 Effective entry date 6
# 76    Settlement date    3  (filled by bank, spaces)
# 79    Originator status  1
# 80-87 ODFI routing       8
# 88-94 Batch number       7

$batchHeader = "5" +
               "200" +
               $CompanyName +
               "                    " +
               $CompanyId +
               "PPD" +
               $CompanyEntryDesc +
               "      " +
               $effectiveDate +
               "   " +
               "1" +
               $ODFIRouting +
               $BatchNumber
Assert94 $batchHeader "BatchHeader"
$records.Add($batchHeader)

# --- Entry Detail records (type 6) ---
# 1     Record type        1
# 2-3   Transaction code   2
# 4-11  RDFI routing (8)   8
# 12    Check digit        1
# 13-29 RDFI account      17  (right-justified, space-padded)
# 30-39 Amount            10  (cents, zero-padded)
# 40-54 Individual ID     15
# 55-76 Individual name   22
# 77-78 Discretionary     2   (spaces)
# 79    Addenda indicator  1   (0 = no addenda)
# 80-94 Trace number      15  (ODFI 8 + sequence 7)

foreach ($e in $entries) {
    $rdfiRouting   = $e.Routing.Substring(0,8)
    $rdfiCheckDigit = $e.Routing[8]
    $accountField  = $e.Account.PadLeft(17)
    $amountField   = $e.Cents.ToString().PadLeft(10,'0')
    $individualId  = PadRight "" 15          # blank individual ID
    $individualName= PadRight $e.Name 22
    $traceSeqStr   = $e.TraceSeq.ToString().PadLeft(7,'0')
    $traceNumber   = $ODFIRouting + $traceSeqStr

    $entry = "6" +
             $e.TxCode +
             $rdfiRouting +
             $rdfiCheckDigit +
             $accountField +
             $amountField +
             $individualId +
             $individualName +
             "  " +
             "0" +
             $traceNumber
    Assert94 $entry "Entry $($e.TraceSeq)"
    $records.Add($entry)
}

# --- Batch Control (type 8) ---
# 1     Record type        1
# 2-4   Service class      3
# 5-10  Entry/addenda count 6
# 11-20 Entry hash        10
# 21-32 Total debit       12
# 33-44 Total credit      12
# 45-54 Company ID        10
# 55-73 Message auth code 19  (spaces)
# 74-79 Reserved           6  (spaces)
# 80-87 ODFI routing       8
# 88-94 Batch number       7

$batchControl = "8" +
                "200" +
                $entryAddendaCount.ToString().PadLeft(6,'0') +
                $batchEntryHashStr +
                $batchDebitStr +
                $batchCreditStr +
                $CompanyId +
                "                   " +
                "      " +
                $ODFIRouting +
                $BatchNumber
Assert94 $batchControl "BatchControl"
$records.Add($batchControl)

# --- File Control (type 9) ---
# 1     Record type        1
# 2-4   Batch count        6
# 5-10  Block count        6  (number of 10-record blocks, ceil to nearest 10)
# 11-20 Entry/addenda count 8
# 21-30 Entry hash        10
# 31-42 Total debit       12
# 43-54 Total credit      12
# 55-94 Reserved          39  (spaces)

$totalRecords = $records.Count + 2   # +2 for this record + padding
$blockCount   = [math]::Ceiling($totalRecords / 10)

$fileControl = "9" +
               "000001" +
               $blockCount.ToString().PadLeft(6,'0') +
               $entryAddendaCount.ToString().PadLeft(8,'0') +
               $batchEntryHashStr +
               $batchDebitStr +
               $batchCreditStr +
               "                                       "
Assert94 $fileControl "FileControl"
$records.Add($fileControl)

# Pad file to a multiple of 10 records with "9" * 94
while ($records.Count % 10 -ne 0) {
    $records.Add("9" * 94)
}

# ---------------------------------------------------------------------------
# Write output
# ---------------------------------------------------------------------------
if (-not $OutputPath) {
    $timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutputPath = Join-Path $PSScriptRoot "sample_${timestamp}.ach"
}

$outputDir = Split-Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$records | Set-Content -Path $OutputPath -Encoding ASCII -NoNewline -ErrorAction Stop
# NACHA requires CRLF line endings
$content = $records -join "`r`n"
[System.IO.File]::WriteAllText($OutputPath, ($content + "`r`n"), [System.Text.Encoding]::ASCII)

Write-Host "Generated NACHA file: $OutputPath"
Write-Host "  Entry records : $entryCount"
Write-Host "  Total records : $($records.Count)  ($($records.Count / 10) blocks)"
Write-Host "  Credit total  : `$$("{0:N2}" -f ($batchCreditTotal / 100))"
Write-Host "  Debit total   : `$$("{0:N2}" -f ($batchDebitTotal  / 100))"
