/**
 * Minimal, dependency-free .xlsx writer (Office Open XML in an uncompressed ZIP).
 * Supports several sheets, a bold header row, frozen header, auto-filter, column widths,
 * EGP money formatting and bold total rows. Opens in Excel, Google Sheets and Numbers.
 * Cells can be formulas ({ f: 'SUM(Data!F2:F99)' }), recalculated when the file opens.
 * `dashboard(...)` builds a "Dashboard" sheet over a data sheet: KPIs and summary tables made of formulas, plus native
 * Excel charts — so the numbers update by themselves when the data changes.
 *
 *   SA.core.xlsx.download('bookings.xlsx', [
 *     { name: 'Bookings', columns: [{ header: 'ID', width: 16 }, { header: 'Amount', width: 12, type: 'money' }],
 *       rows: [['SA-1', 650]], totals: ['Total', 650] },
 *   ]);
 */
(function (SA) {
  // ---------- CRC-32 ----------
  const CRC = new Uint32Array(256);
  for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; CRC[n] = c >>> 0; }
  const crc32 = (bytes) => { let c = 0xffffffff; for (let i = 0; i < bytes.length; i++) c = CRC[(c ^ bytes[i]) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };

  // ---------- ZIP (store method, UTF-8 names) ----------
  const zip = (files) => {
    const enc = new TextEncoder();
    const parts = [], central = [];
    let offset = 0;
    const now = new Date();
    const dosTime = (now.getHours() << 11) | (now.getMinutes() << 5) | (now.getSeconds() >> 1);
    const dosDate = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();
    files.forEach(({ name, data }) => {
      const nameB = enc.encode(name), dataB = typeof data === 'string' ? enc.encode(data) : data;
      const crc = crc32(dataB);
      const local = new DataView(new ArrayBuffer(30));
      local.setUint32(0, 0x04034b50, true); local.setUint16(4, 20, true); local.setUint16(6, 0x0800, true); local.setUint16(8, 0, true);
      local.setUint16(10, dosTime, true); local.setUint16(12, dosDate, true); local.setUint32(14, crc, true);
      local.setUint32(18, dataB.length, true); local.setUint32(22, dataB.length, true); local.setUint16(26, nameB.length, true); local.setUint16(28, 0, true);
      parts.push(new Uint8Array(local.buffer), nameB, dataB);
      const cen = new DataView(new ArrayBuffer(46));
      cen.setUint32(0, 0x02014b50, true); cen.setUint16(4, 20, true); cen.setUint16(6, 20, true); cen.setUint16(8, 0x0800, true); cen.setUint16(10, 0, true);
      cen.setUint16(12, dosTime, true); cen.setUint16(14, dosDate, true); cen.setUint32(16, crc, true); cen.setUint32(20, dataB.length, true); cen.setUint32(24, dataB.length, true);
      cen.setUint16(28, nameB.length, true); cen.setUint16(30, 0, true); cen.setUint16(32, 0, true); cen.setUint16(34, 0, true); cen.setUint16(36, 0, true); cen.setUint32(38, 0, true); cen.setUint32(42, offset, true);
      central.push(new Uint8Array(cen.buffer), nameB);
      offset += 30 + nameB.length + dataB.length;
    });
    const cenSize = central.reduce((a, b) => a + b.length, 0);
    const end = new DataView(new ArrayBuffer(22));
    end.setUint32(0, 0x06054b50, true); end.setUint16(8, files.length, true); end.setUint16(10, files.length, true);
    end.setUint32(12, cenSize, true); end.setUint32(16, offset, true);
    return new Blob([...parts, ...central, new Uint8Array(end.buffer)], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  };

  // ---------- Sheet XML ----------
  const esc = (s) => String(s).replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  const colName = (i) => { let s = ''; i++; while (i) { const m = (i - 1) % 26; s = String.fromCharCode(65 + m) + s; i = Math.floor((i - 1) / 26); } return s; };
  // Style ids defined in styles.xml below
  const ST = { text: 0, header: 1, money: 2, totalText: 3, totalMoney: 4, number: 5, totalNumber: 6, percent: 7, title: 8, kpiLabel: 9, kpiMoney: 10, kpiNumber: 11, kpiPercent: 12, note: 13 };

  const cell = (ref, v, style) => {
    if (v === null || v === undefined || v === '') return `<c r="${ref}" s="${style}"/>`;
    if (typeof v === 'object' && v.f) return `<c r="${ref}" s="${v.s != null ? ST[v.s] ?? v.s : style}"><f>${esc(v.f)}</f></c>`;
    if (typeof v === 'number' && isFinite(v)) return `<c r="${ref}" s="${style}"><v>${v}</v></c>`;
    return `<c r="${ref}" s="${style}" t="inlineStr"><is><t xml:space="preserve">${esc(v)}</t></is></c>`;
  };

  const sheetXml = ({ columns, rows, totals, title }) => {
    const out = [];
    let r = 1;
    const styleFor = (col, isTotal) => {
      if (col.type === 'money') return isTotal ? ST.totalMoney : ST.money;
      if (col.type === 'number') return isTotal ? ST.totalNumber : ST.number;
      if (col.type === 'percent') return ST.percent;
      return isTotal ? ST.totalText : ST.text;
    };
    if (title) { out.push(`<row r="${r}" ht="24" customHeight="1">${cell(`A${r}`, title, ST.title)}</row>`); r += 2; }
    const headerRow = r;
    out.push(`<row r="${r}">${columns.map((c, i) => cell(`${colName(i)}${r}`, c.header, ST.header)).join('')}</row>`); r++;
    rows.forEach((row) => { out.push(`<row r="${r}">${columns.map((c, i) => cell(`${colName(i)}${r}`, row[i], styleFor(c, false))).join('')}</row>`); r++; });
    if (totals) { out.push(`<row r="${r}">${columns.map((c, i) => cell(`${colName(i)}${r}`, totals[i], styleFor(c, true))).join('')}</row>`); }
    const lastCol = colName(columns.length - 1);
    const lastDataRow = Math.max(headerRow, headerRow + rows.length);
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheetViews><sheetView workbookViewId="0"><pane ySplit="${headerRow}" topLeftCell="A${headerRow + 1}" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>
<sheetFormatPr defaultRowHeight="15"/>
<cols>${columns.map((c, i) => `<col min="${i + 1}" max="${i + 1}" width="${c.width || 14}" customWidth="1"/>`).join('')}</cols>
<sheetData>${out.join('')}</sheetData>
${rows.length ? `<autoFilter ref="A${headerRow}:${lastCol}${lastDataRow}"/>` : ''}
</worksheet>`;
  };

  /** A free-form sheet: grid = rows of cells ({ v, s } or plain values / formulas), with optional drawing (charts) */
  const gridXml = ({ cols = [], grid, drawing }) => {
    const out = grid.map((row, ri) => `<row r="${ri + 1}"${row.h ? ` ht="${row.h}" customHeight="1"` : ''}>${(row.cells || row).map((c, ci) => {
      if (c == null) return '';
      const style = typeof c === 'object' && !c.f && 's' in c ? ST[c.s] ?? 0 : typeof c === 'object' && c.f ? (ST[c.s] ?? 0) : 0;
      const v = typeof c === 'object' && !c.f && 'v' in c ? c.v : c;
      return cell(`${colName(ci)}${ri + 1}`, v, style);
    }).join('')}</row>`);
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheetViews><sheetView workbookViewId="0" showGridLines="0"/></sheetViews><sheetFormatPr defaultRowHeight="15"/>
${cols.length ? `<cols>${cols.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('')}</cols>` : ''}
<sheetData>${out.join('')}</sheetData>${drawing ? '<drawing r:id="rId1"/>' : ''}
</worksheet>`;
  };
  const PALETTE = ['0B6E4F', 'FF5B14', 'C9963A', '1C2541', '6C3FB5', '0E7C86', 'E4572E', '17795A'];
  /** One native chart (bar, line or pie) — refs are full ranges like 'Dashboard'!$A$5:$A$12 */
  const chartXml = ({ type = 'bar', title, cats, series }) => {
    const ser = series.map((x, i) => `<c:ser><c:idx val="${i}"/><c:order val="${i}"/><c:tx><c:v>${esc(x.name)}</c:v></c:tx>
      ${type === 'pie' ? '' : `<c:spPr>${type === 'line' ? `<a:ln w="28575"><a:solidFill><a:srgbClr val="${PALETTE[i % 8]}"/></a:solidFill></a:ln>` : `<a:solidFill><a:srgbClr val="${PALETTE[i % 8]}"/></a:solidFill>`}</c:spPr>`}
      ${type === 'pie' ? `${x.points.map((_, k) => `<c:dPt><c:idx val="${k}"/><c:bubble3D val="0"/><c:spPr><a:solidFill><a:srgbClr val="${PALETTE[k % 8]}"/></a:solidFill></c:spPr></c:dPt>`).join('')}<c:dLbls><c:showLegendKey val="0"/><c:showVal val="0"/><c:showCatName val="0"/><c:showSerName val="0"/><c:showPercent val="1"/><c:showBubbleSize val="0"/></c:dLbls>` : ''}
      <c:cat><c:strRef><c:f>${esc(cats)}</c:f></c:strRef></c:cat><c:val><c:numRef><c:f>${esc(x.ref)}</c:f></c:numRef></c:val>${type === 'line' ? '<c:smooth val="0"/>' : ''}</c:ser>`).join('');
    const axes = type === 'pie' ? '' : '<c:axId val="111"/><c:axId val="222"/>';
    const plot = type === 'pie' ? `<c:pieChart><c:varyColors val="1"/>${ser}<c:firstSliceAng val="0"/></c:pieChart>`
      : type === 'line' ? `<c:lineChart><c:grouping val="standard"/><c:varyColors val="0"/>${ser}<c:marker val="1"/>${axes}</c:lineChart>`
      : `<c:barChart><c:barDir val="col"/><c:grouping val="clustered"/><c:varyColors val="0"/>${ser}<c:gapWidth val="60"/>${axes}</c:barChart>`;
    const axXml = type === 'pie' ? '' : `<c:catAx><c:axId val="111"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/><c:axPos val="b"/><c:numFmt formatCode="General" sourceLinked="1"/><c:tickLblPos val="nextTo"/><c:crossAx val="222"/><c:crosses val="autoZero"/><c:auto val="1"/><c:lblAlgn val="ctr"/><c:lblOffset val="100"/></c:catAx>
      <c:valAx><c:axId val="222"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/><c:axPos val="l"/><c:majorGridlines><c:spPr><a:ln><a:solidFill><a:srgbClr val="E3E7E4"/></a:solidFill></a:ln></c:spPr></c:majorGridlines><c:numFmt formatCode="#,##0" sourceLinked="0"/><c:tickLblPos val="nextTo"/><c:crossAx val="111"/><c:crosses val="autoZero"/><c:crossBetween val="between"/></c:valAx>`;
    return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<c:roundedCorners val="0"/><c:chart><c:title><c:tx><c:rich><a:bodyPr/><a:p><a:pPr><a:defRPr sz="1200" b="1"/></a:pPr><a:r><a:rPr lang="en-US" sz="1200" b="1"/><a:t>${esc(title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val="0"/></c:title>
<c:autoTitleDeleted val="0"/><c:plotArea><c:layout/>${plot}${axXml}</c:plotArea><c:legend><c:legendPos val="${type === 'pie' ? 'r' : 'b'}"/><c:overlay val="0"/></c:legend><c:plotVisOnly val="1"/><c:dispBlanksAs val="gap"/></c:chart></c:chartSpace>`;
  };
  const drawingXml = (charts) => `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
${charts.map((ch, i) => `<xdr:twoCellAnchor><xdr:from><xdr:col>${ch.at[0]}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>${ch.at[1]}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:to><xdr:col>${ch.at[0] + (ch.size?.[0] || 7)}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>${ch.at[1] + (ch.size?.[1] || 16)}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
<xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="${i + 2}" name="Chart ${i + 1}"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr><xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart r:id="rId${i + 1}"/></a:graphicData></a:graphic></xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor>`).join('')}
</xdr:wsDr>`;

  const STYLES = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0 &quot;EGP&quot;"/><numFmt numFmtId="165" formatCode="0.0%"/></numFmts>
<fonts count="6"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="15"/><color rgb="FF0D1411"/><name val="Calibri"/></font><font><b/><sz val="16"/><color rgb="FF0B6E4F"/><name val="Calibri"/></font><font><i/><sz val="9"/><color rgb="FF6B746F"/><name val="Calibri"/></font></fonts>
<fills count="5"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF0B6E4F"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFEBDF"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEEF5F0"/><bgColor indexed="64"/></patternFill></fill></fills>
<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top style="thin"><color rgb="FF0D1411"/></top><bottom style="double"><color rgb="FF0D1411"/></bottom><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="14">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
<xf numFmtId="164" fontId="2" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
<xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="3" fontId="2" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
<xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
<xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
<xf numFmtId="0" fontId="2" fillId="4" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
<xf numFmtId="164" fontId="4" fillId="4" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1"/>
<xf numFmtId="3" fontId="4" fillId="4" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1"/>
<xf numFmtId="165" fontId="4" fillId="4" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1"/>
<xf numFmtId="0" fontId="5" fillId="0" borderId="0" xfId="0" applyFont="1"/>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>`;

  const safeSheetName = (n, used) => {
    let s = String(n || 'Sheet').replace(/[\[\]:*?/\\]/g, ' ').slice(0, 31).trim() || 'Sheet';
    let k = 2; const base = s;
    while (used.has(s)) s = `${base.slice(0, 28)} ${k++}`;
    used.add(s); return s;
  };

  /** Build the workbook Blob */
  const build = (sheets) => {
    const used = new Set();
    const named = sheets.map((s) => ({ ...s, name: safeSheetName(s.name, used) }));
    const files = [
      { name: '[Content_Types].xml', data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>${named.map((_, i) => `<Override PartName="/xl/worksheets/sheet${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`).join('')}<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>` },
      { name: '_rels/.rels', data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>` },
      { name: 'docProps/core.xml', data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>Super App export</dc:title><dc:creator>Super App Partner Dashboard</dc:creator><dcterms:created xsi:type="dcterms:W3CDTF">${new Date().toISOString().replace(/\.\d{3}Z$/, 'Z')}</dcterms:created></cp:coreProperties>` },
      { name: 'xl/workbook.xml', data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets>${named.map((s, i) => `<sheet name="${esc(s.name)}" sheetId="${i + 1}" r:id="rId${i + 1}"/>`).join('')}</sheets>${named.some((s) => s.rows?.length && !s.grid) ? `<definedNames>${named.map((s, i) => (s.rows?.length && !s.grid ? `<definedName name="_xlnm._FilterDatabase" localSheetId="${i}" hidden="1">'${esc(s.name).replace(/'/g, "''")}'!$A$${s.title ? 3 : 1}:$${colName(s.columns.length - 1)}$${(s.title ? 3 : 1) + s.rows.length}</definedName>` : '')).join('')}</definedNames>` : ''}<calcPr calcId="191029" fullCalcOnLoad="1"/></workbook>` },
      { name: 'xl/_rels/workbook.xml.rels', data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${named.map((_, i) => `<Relationship Id="rId${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i + 1}.xml"/>`).join('')}<Relationship Id="rId${named.length + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>` },
      { name: 'xl/styles.xml', data: STYLES },
      ...named.map((s, i) => ({ name: `xl/worksheets/sheet${i + 1}.xml`, data: s.grid ? gridXml({ ...s, drawing: !!s.charts?.length }) : sheetXml(s) })),
    ];
    // Charts: one drawing per sheet that has charts, one chart part per chart
    let chartNo = 0, drawNo = 0;
    named.forEach((s, i) => {
      if (!s.charts?.length) return;
      drawNo++;
      const first = chartNo + 1;
      s.charts.forEach((ch) => { chartNo++; files.push({ name: `xl/charts/chart${chartNo}.xml`, data: chartXml(ch) }); });
      files.push({ name: `xl/drawings/drawing${drawNo}.xml`, data: drawingXml(s.charts) });
      files.push({ name: `xl/drawings/_rels/drawing${drawNo}.xml.rels`, data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${s.charts.map((_, k) => `<Relationship Id="rId${k + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart${first + k}.xml"/>`).join('')}</Relationships>` });
      files.push({ name: `xl/worksheets/_rels/sheet${i + 1}.xml.rels`, data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing${drawNo}.xml"/></Relationships>` });
    });
    if (chartNo) {
      const ct = files[0];
      ct.data = ct.data.replace('</Types>', `${Array.from({ length: drawNo }, (_, k) => `<Override PartName="/xl/drawings/drawing${k + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>`).join('')}${Array.from({ length: chartNo }, (_, k) => `<Override PartName="/xl/charts/chart${k + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>`).join('')}</Types>`);
    }
    return zip(files);
  };

  const download = (filename, sheets) => {
    const blob = build(sheets);
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename.endsWith('.xlsx') ? filename : `${filename}.xlsx`;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 4000);
    return blob;
  };

  /**
   * A workbook with a live "Dashboard" first, then the data sheet(s).
   * spec: { title, subtitle, data: { name, columns, rows }, more: [other sheets],
   *   kpis: [{ label, f: 'SUM' | 'COUNT' | 'AVERAGE' | 'COUNTIF' | formula string, col: header name, match?, type: money|number|percent }],
   *   groups: [{ title, by: header name, sum?: header name (or count), chart: bar|pie|line, type: money|number }] }
   * Every number on the dashboard is an Excel formula over the data sheet.
   */
  const dashboard = ({ title, subtitle = '', data, more = [], kpis = [], groups = [] }) => {
    const DS = safeSheetName(data.name, new Set(['Dashboard']));
    const q = `'${DS.replace(/'/g, "''")}'`;
    const last = data.rows.length + 1 + 2000; // room for rows added later
    const col = (h) => { const i = data.columns.findIndex((c) => c.header === h); if (i < 0) throw new Error(`No column ${h}`); return colName(i); };
    const rng = (h) => `${q}!$${col(h)}$2:$${col(h)}$${last}`;
    const grid = [];
    grid.push({ h: 28, cells: [{ v: title, s: 'title' }] });
    grid.push([{ v: `${subtitle}${subtitle ? ' · ' : ''}Made ${new Date().toLocaleString('en-GB')} · every number below is a formula over the “${DS}” sheet`, s: 'note' }]);
    grid.push([]);
    // KPIs: label row + value row, 2 columns each
    const kRow = grid.length;
    const labels = [], values = [];
    kpis.forEach((k) => {
      const f = k.formula || (k.f === 'COUNT' ? `COUNTA(${rng(k.col)})` : k.f === 'COUNTIF' ? `COUNTIF(${rng(k.col)},"${k.match}")` : k.f === 'SUMIF' ? `SUMIF(${rng(k.col)},"${k.match}",${rng(k.sum)})` : `${k.f || 'SUM'}(${rng(k.col)})`);
      labels.push({ v: k.label, s: 'kpiLabel' });
      values.push({ f, s: k.type === 'money' ? 'kpiMoney' : k.type === 'percent' ? 'kpiPercent' : 'kpiNumber' });
    });
    grid.push(labels); grid.push({ h: 26, cells: values }); grid.push([]);
    // Groups: a small table per group (labels from the data, values by formula) + a chart next to it
    const charts = [];
    let r = grid.length;
    groups.forEach((g) => {
      const byIdx = data.columns.findIndex((c) => c.header === g.by);
      const keys = [...new Set(data.rows.map((x) => x[byIdx]).filter((x) => x !== '' && x != null))].slice(0, g.limit || 24);
      if (!keys.length) return;
      grid.push([{ v: g.title, s: 'header' }, { v: g.sum ? g.sum : 'Count', s: 'header' }]);
      const top = grid.length + 1;
      keys.forEach((k) => grid.push([{ v: String(k), s: 'text' }, { f: g.sum ? `SUMIF(${rng(g.by)},A${grid.length + 1},${rng(g.sum)})` : `COUNTIF(${rng(g.by)},A${grid.length + 1})`, s: g.type === 'money' ? 'money' : 'number' }]));
      const bottom = grid.length;
      charts.push({ type: g.chart || 'bar', title: g.title, cats: `'Dashboard'!$A$${top}:$A$${bottom}`, series: [{ name: g.sum || 'Count', ref: `'Dashboard'!$B$${top}:$B$${bottom}`, points: keys }], at: [3, r], size: [8, Math.max(14, keys.length + 3)] });
      grid.push([]);
      r = grid.length;
      // keep each chart below the previous one
      const need = charts[charts.length - 1].at[1] + charts[charts.length - 1].size[1];
      while (grid.length < need) grid.push([]);
      r = grid.length;
    });
    void kRow;
    return [{ name: 'Dashboard', grid, cols: [28, 20, 20, 20, 20, 20, 20, 20, 20], charts }, { ...data, name: DS }, ...more];
  };

  SA.core.xlsx = { build, download, dashboard };
})(window.SA);
