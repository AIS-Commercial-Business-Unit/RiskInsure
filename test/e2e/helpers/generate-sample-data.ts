const sampleAccounts = [
  // Chase Bank (021000021)
  { routing: '021000021', account: '4520001001', name: 'JAMES ANDERSON' },
  { routing: '021000021', account: '4520001002', name: 'MARIA GARCIA' },
  { routing: '021000021', account: '4520001003', name: 'ROBERT JOHNSON' },
  { routing: '021000021', account: '4520001004', name: 'LINDA MARTINEZ' },
  { routing: '021000021', account: '4520001005', name: 'WILLIAM DAVIS' },
  // Bank of America (026009593)
  { routing: '026009593', account: '8831002001', name: 'BARBARA WILSON' },
  { routing: '026009593', account: '8831002002', name: 'RICHARD MOORE' },
  { routing: '026009593', account: '8831002003', name: 'SUSAN TAYLOR' },
  { routing: '026009593', account: '8831002004', name: 'JOSEPH JACKSON' },
  { routing: '026009593', account: '8831002005', name: 'JESSICA WHITE' },
  // Wells Fargo (121042882)
  { routing: '121042882', account: '6610003001', name: 'THOMAS HARRIS' },
  { routing: '121042882', account: '6610003002', name: 'SARAH MARTIN' },
  { routing: '121042882', account: '6610003003', name: 'CHARLES THOMPSON' },
  { routing: '121042882', account: '6610003004', name: 'KAREN GARCIA' },
  { routing: '121042882', account: '6610003005', name: 'CHRISTOPHER MARTINEZ' },
  // Citibank (021000089)
  { routing: '021000089', account: '3340004001', name: 'PATRICIA ROBINSON' },
  { routing: '021000089', account: '3340004002', name: 'DANIEL CLARK' },
  { routing: '021000089', account: '3340004003', name: 'NANCY RODRIGUEZ' },
  { routing: '021000089', account: '3340004004', name: 'MARK LEWIS' },
  { routing: '021000089', account: '3340004005', name: 'BETTY LEE' },
  // US Bank (091000022)
  { routing: '091000022', account: '7720005001', name: 'PAUL WALKER' },
  { routing: '091000022', account: '7720005002', name: 'DOROTHY HALL' },
  { routing: '091000022', account: '7720005003', name: 'GEORGE ALLEN' },
  { routing: '091000022', account: '7720005004', name: 'SANDRA YOUNG' },
  { routing: '091000022', account: '7720005005', name: 'KENNETH HERNANDEZ' },
  // PNC Bank (043000096)
  { routing: '043000096', account: '5550006001', name: 'HELEN KING' },
  { routing: '043000096', account: '5550006002', name: 'DONALD WRIGHT' },
  { routing: '043000096', account: '5550006003', name: 'CAROL LOPEZ' },
  { routing: '043000096', account: '5550006004', name: 'STEVEN HILL' },
  { routing: '043000096', account: '5550006005', name: 'RUTH SCOTT' },
  // TD Bank (031101266)
  { routing: '031101266', account: '2200007001', name: 'LARRY GREEN' },
  { routing: '031101266', account: '2200007002', name: 'SHARON ADAMS' },
  { routing: '031101266', account: '2200007003', name: 'EDWARD BAKER' },
  { routing: '031101266', account: '2200007004', name: 'VIRGINIA GONZALEZ' },
  { routing: '031101266', account: '2200007005', name: 'JASON NELSON' },
  // Capital One (056073502)
  { routing: '056073502', account: '9980008001', name: 'DEBORAH CARTER' },
  { routing: '056073502', account: '9980008002', name: 'RYAN MITCHELL' },
  { routing: '056073502', account: '9980008003', name: 'KATHLEEN PEREZ' },
  { routing: '056073502', account: '9980008004', name: 'GARY ROBERTS' },
  { routing: '056073502', account: '9980008005', name: 'AMY TURNER' },
  // Truist Bank (061000104)
  { routing: '061000104', account: '1110009001', name: 'PETER PHILLIPS' },
  { routing: '061000104', account: '1110009002', name: 'ANGELA CAMPBELL' },
  { routing: '061000104', account: '1110009003', name: 'HAROLD PARKER' },
  { routing: '061000104', account: '1110009004', name: 'STEPHANIE EVANS' },
  { routing: '061000104', account: '1110009005', name: 'WALTER EDWARDS' },
  // Fifth Third Bank (042000314)
  { routing: '042000314', account: '4430010001', name: 'DIANA COLLINS' },
  { routing: '042000314', account: '4430010002', name: 'JONATHAN STEWART' },
  { routing: '042000314', account: '4430010003', name: 'BRENDA SANCHEZ' },
  { routing: '042000314', account: '4430010004', name: 'RAYMOND MORRIS' },
  { routing: '042000314', account: '4430010005', name: 'VIRGINIA ROGERS' },
  // KeyBank (041001039)
  { routing: '041001039', account: '6670011001', name: 'SAMUEL REED' },
  { routing: '041001039', account: '6670011002', name: 'WAYNE COOK' },
  { routing: '041001039', account: '6670011003', name: 'THERESA MORGAN' },
  { routing: '041001039', account: '6670011004', name: 'ARTHUR BELL' },
  { routing: '041001039', account: '6670011005', name: 'JUDITH MURPHY' },
  // Regions Bank (062000019)
  { routing: '062000019', account: '8890012001', name: 'FRED BAILEY' },
  { routing: '062000019', account: '8890012002', name: 'VIRGINIA RIVERA' },
  { routing: '062000019', account: '8890012003', name: 'GERALD COOPER' },
  { routing: '062000019', account: '8890012004', name: 'AMANDA RICHARDSON' },
  { routing: '062000019', account: '8890012005', name: 'ALAN COX' },
  // Huntington Bank (044000024)
  { routing: '044000024', account: '3320013001', name: 'MILDRED HOWARD' },
  { routing: '044000024', account: '3320013002', name: 'CARL WARD' },
  { routing: '044000024', account: '3320013003', name: 'CHERYL TORRES' },
  { routing: '044000024', account: '3320013004', name: 'BOBBY PETERSON' },
  { routing: '044000024', account: '3320013005', name: 'VIRGINIA GRAY' },
  // Citizens Bank (011500010)
  { routing: '011500010', account: '7750014001', name: 'PHILIP RAMIREZ' },
  { routing: '011500010', account: '7750014002', name: 'DIANE JAMES' },
  { routing: '011500010', account: '7750014003', name: 'RUSSELL WATSON' },
  { routing: '011500010', account: '7750014004', name: 'SHIRLEY BROOKS' },
  { routing: '011500010', account: '7750014005', name: 'RALPH KELLY' },
  // Ally Bank (124003116)
  { routing: '124003116', account: '5560015001', name: 'JOAN SANDERS' },
  { routing: '124003116', account: '5560015002', name: 'BRUCE PRICE' },
  { routing: '124003116', account: '5560015003', name: 'EVELYN BENNETT' },
  { routing: '124003116', account: '5560015004', name: 'ROY WOOD' },
  { routing: '124003116', account: '5560015005', name: 'VIRGINIA BARNES' },
  // First Horizon (084000026)
  { routing: '084000026', account: '2210016001', name: 'JACK ROSS' },
  { routing: '084000026', account: '2210016002', name: 'JULIA HENDERSON' },
  { routing: '084000026', account: '2210016003', name: 'TERRY COLEMAN' },
  { routing: '084000026', account: '2210016004', name: 'MARILYN JENKINS' },
  { routing: '084000026', account: '2210016005', name: 'DENNIS PERRY' },
  // BBVA (062001186)
  { routing: '062001186', account: '9910017001', name: 'REBECCA POWELL' },
  { routing: '062001186', account: '9910017002', name: 'SEAN LONG' },
  { routing: '062001186', account: '9910017003', name: 'DORIS PATTERSON' },
  { routing: '062001186', account: '9910017004', name: 'RAYMOND HUGHES' },
  { routing: '062001186', account: '9910017005', name: 'VIRGINIA FLORES' },
  // East West Bank (322070381)
  { routing: '322070381', account: '6640018001', name: 'HAROLD WASHINGTON' },
  { routing: '322070381', account: '6640018002', name: 'GLORIA BUTLER' },
  { routing: '322070381', account: '6640018003', name: 'EARL SIMMONS' },
  { routing: '322070381', account: '6640018004', name: 'VIRGINIA FOSTER' },
  { routing: '322070381', account: '6640018005', name: 'BOBBY GONZALES' },
  // Flagstar Bank (272471548)
  { routing: '272471548', account: '4450019001', name: 'MILDRED BRYANT' },
  { routing: '272471548', account: '4450019002', name: 'CLARENCE ALEXANDER' },
  { routing: '272471548', account: '4450019003', name: 'VIRGINIA RUSSELL' },
  { routing: '272471548', account: '4450019004', name: 'FRANK GRIFFIN' },
  { routing: '272471548', account: '4450019005', name: 'LOUISE DIAZ' },
  // Glacier Bank (092905278)
  { routing: '092905278', account: '7780020001', name: 'HENRY HAYES' },
  { routing: '092905278', account: '7780020002', name: 'VIRGINIA MYERS' },
  { routing: '092905278', account: '7780020003', name: 'RALPH FORD' },
  { routing: '092905278', account: '7780020004', name: 'VIRGINIA HAMILTON' },
  { routing: '092905278', account: '7780020005', name: 'RUSSELL GRAHAM' },
];

const txCodes = ['22', '22', '22', '27', '32', '37'];

function padRight(value: string, len: number): string {
  return value.length >= len ? value.substring(0, len) : value.padEnd(len);
}

export function generateNachaFileContent(): string {
  // NACHA config
  const immediateDestination = ' 021000021';   // leading space + 9-digit routing
  const immediateOrigin      = '1234567890';
  const immediateDestName    = padRight('JPMORGAN CHASE', 23);
  const immediateOriginName  = padRight('ACME CORP', 23);
  const companyName          = padRight('ACME CORP', 16);
  const companyId            = '1234567890';
  const companyEntryDesc     = padRight('PAYROLL', 10);
  const odfiRouting          = '02100002';
  const batchNumber          = '0000001';

  // Date values
  const now = new Date();
  const yy  = String(now.getFullYear()).slice(2);
  const mo  = String(now.getMonth() + 1).padStart(2, '0');
  const dd  = String(now.getDate()).padStart(2, '0');
  const hh  = String(now.getHours()).padStart(2, '0');
  const mm  = String(now.getMinutes()).padStart(2, '0');
  const fileDate    = `${yy}${mo}${dd}`;
  const fileTime    = `${hh}${mm}`;
  const tomorrow    = new Date(now);
  tomorrow.setDate(tomorrow.getDate() + 1);
  const tyy = String(tomorrow.getFullYear()).slice(2);
  const tmo = String(tomorrow.getMonth() + 1).padStart(2, '0');
  const tdd = String(tomorrow.getDate()).padStart(2, '0');
  const effectiveDate = `${tyy}${tmo}${tdd}`;

  // Random entry count 20..40 (inclusive), same as PowerShell script
  const entryCount = Math.floor(Math.random() * 21) + 20;

  const entries = Array.from({ length: entryCount }, (_, i) => {
    const acct   = sampleAccounts[Math.floor(Math.random() * sampleAccounts.length)];
    const cents  = Math.floor(Math.random() * 90001) + 10000; // $100.00 – $1000.00
    const txCode = txCodes[Math.floor(Math.random() * txCodes.length)];
    return { ...acct, cents, txCode, traceSeq: i + 1 };
  });

  // Batch totals
  let batchDebitTotal  = 0;
  let batchCreditTotal = 0;
  let batchEntryHash   = BigInt(0);
  for (const e of entries) {
    if (e.txCode === '27' || e.txCode === '37') batchDebitTotal  += e.cents;
    else                                         batchCreditTotal += e.cents;
    batchEntryHash += BigInt(e.routing.substring(0, 8));
  }
  const batchEntryHashStr = (batchEntryHash % BigInt(10000000000)).toString().padStart(10, '0');
  const batchDebitStr     = batchDebitTotal.toString().padStart(12, '0');
  const batchCreditStr    = batchCreditTotal.toString().padStart(12, '0');

  const records: string[] = [];

  // File Header (type 1) — 94 chars
  records.push(
    '1' +
    '01' +
    immediateDestination +
    immediateOrigin +
    fileDate +
    fileTime +
    'A' +
    '094' +
    '10' +
    '1' +
    immediateDestName +
    immediateOriginName +
    '        '
  );

  // Batch Header (type 5) — 94 chars
  records.push(
    '5' +
    '200' +
    companyName +
    '                    ' +
    companyId +
    'PPD' +
    companyEntryDesc +
    '      ' +
    effectiveDate +
    '   ' +
    '1' +
    odfiRouting +
    batchNumber
  );

  // Entry Detail records (type 6) — 94 chars each
  for (const e of entries) {
    const rdfiRouting    = e.routing.substring(0, 8);
    const rdfiCheckDigit = e.routing[8];
    const accountField   = e.account.padStart(17);
    const amountField    = e.cents.toString().padStart(10, '0');
    const individualId   = padRight('', 15);
    const individualName = padRight(e.name, 22);
    const traceNumber    = odfiRouting + e.traceSeq.toString().padStart(7, '0');
    records.push(
      '6' +
      e.txCode +
      rdfiRouting +
      rdfiCheckDigit +
      accountField +
      amountField +
      individualId +
      individualName +
      '  ' +
      '0' +
      traceNumber
    );
  }

  // Batch Control (type 8) — 94 chars
  records.push(
    '8' +
    '200' +
    entryCount.toString().padStart(6, '0') +
    batchEntryHashStr +
    batchDebitStr +
    batchCreditStr +
    companyId +
    '                   ' +
    '      ' +
    odfiRouting +
    batchNumber
  );

  // File Control (type 9) — 94 chars
  const totalRecords = records.length + 2; // +1 for this record, +1 buffer (mirrors PowerShell)
  const blockCount   = Math.ceil(totalRecords / 10);
  records.push(
    '9' +
    '000001' +
    blockCount.toString().padStart(6, '0') +
    entryCount.toString().padStart(8, '0') +
    batchEntryHashStr +
    batchDebitStr +
    batchCreditStr +
    '                                       '
  );

  // Pad to a multiple of 10 records with '9' * 94
  while (records.length % 10 !== 0) {
    records.push('9'.repeat(94));
  }

  return records.join('\r\n') + '\r\n';
}
