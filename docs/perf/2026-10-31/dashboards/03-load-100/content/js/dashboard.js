/*
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
var showControllersOnly = false;
var seriesFilter = "";
var filtersOnlySampleSeries = true;

/*
 * Add header in statistics table to group metrics by category
 * format
 *
 */
function summaryTableHeader(header) {
    var newRow = header.insertRow(-1);
    newRow.className = "tablesorter-no-sort";
    var cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 1;
    cell.innerHTML = "Requests";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 3;
    cell.innerHTML = "Executions";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 7;
    cell.innerHTML = "Response Times (ms)";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 1;
    cell.innerHTML = "Throughput";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 2;
    cell.innerHTML = "Network (KB/sec)";
    newRow.appendChild(cell);
}

/*
 * Populates the table identified by id parameter with the specified data and
 * format
 *
 */
function createTable(table, info, formatter, defaultSorts, seriesIndex, headerCreator) {
    var tableRef = table[0];

    // Create header and populate it with data.titles array
    var header = tableRef.createTHead();

    // Call callback is available
    if(headerCreator) {
        headerCreator(header);
    }

    var newRow = header.insertRow(-1);
    for (var index = 0; index < info.titles.length; index++) {
        var cell = document.createElement('th');
        cell.innerHTML = info.titles[index];
        newRow.appendChild(cell);
    }

    var tBody;

    // Create overall body if defined
    if(info.overall){
        tBody = document.createElement('tbody');
        tBody.className = "tablesorter-no-sort";
        tableRef.appendChild(tBody);
        var newRow = tBody.insertRow(-1);
        var data = info.overall.data;
        for(var index=0;index < data.length; index++){
            var cell = newRow.insertCell(-1);
            cell.innerHTML = formatter ? formatter(index, data[index]): data[index];
        }
    }

    // Create regular body
    tBody = document.createElement('tbody');
    tableRef.appendChild(tBody);

    var regexp;
    if(seriesFilter) {
        regexp = new RegExp(seriesFilter, 'i');
    }
    // Populate body with data.items array
    for(var index=0; index < info.items.length; index++){
        var item = info.items[index];
        if((!regexp || filtersOnlySampleSeries && !info.supportsControllersDiscrimination || regexp.test(item.data[seriesIndex]))
                &&
                (!showControllersOnly || !info.supportsControllersDiscrimination || item.isController)){
            if(item.data.length > 0) {
                var newRow = tBody.insertRow(-1);
                for(var col=0; col < item.data.length; col++){
                    var cell = newRow.insertCell(-1);
                    cell.innerHTML = formatter ? formatter(col, item.data[col]) : item.data[col];
                }
            }
        }
    }

    // Add support of columns sort
    table.tablesorter({sortList : defaultSorts});
}

$(document).ready(function() {

    // Customize table sorter default options
    $.extend( $.tablesorter.defaults, {
        theme: 'blue',
        cssInfoBlock: "tablesorter-no-sort",
        widthFixed: true,
        widgets: ['zebra']
    });

    var data = {"OkPercent": 99.98445031876847, "KoPercent": 0.015549681231534754};
    var dataset = [
        {
            "label" : "FAIL",
            "data" : data.KoPercent,
            "color" : "#FF6347"
        },
        {
            "label" : "PASS",
            "data" : data.OkPercent,
            "color" : "#9ACD32"
        }];
    $.plot($("#flot-requests-summary"), dataset, {
        series : {
            pie : {
                show : true,
                radius : 1,
                label : {
                    show : true,
                    radius : 3 / 4,
                    formatter : function(label, series) {
                        return '<div style="font-size:8pt;text-align:center;padding:2px;color:white;">'
                            + label
                            + '<br/>'
                            + Math.round10(series.percent, -2)
                            + '%</div>';
                    },
                    background : {
                        opacity : 0.5,
                        color : '#000'
                    }
                }
            }
        },
        legend : {
            show : true
        }
    });

    // Creates APDEX table
    createTable($("#apdexTable"), {"supportsControllersDiscrimination": true, "overall": {"data": [0.9997166644871627, 500, 1500, "Total"], "isController": false}, "titles": ["Apdex", "T (Toleration threshold)", "F (Frustration threshold)", "Label"], "items": [{"data": [0.9980632666236281, 500, 1500, "T01 Home page - full load"], "isController": true}, {"data": [1.0, 500, 1500, "GET models/tee.glb"], "isController": false}, {"data": [1.0, 500, 1500, "GET og-image.png"], "isController": false}, {"data": [1.0, 500, 1500, "GET sitemap.xml"], "isController": false}, {"data": [0.9996763754045307, 500, 1500, "GET / reload (If-None-Match on every file)"], "isController": false}, {"data": [1.0, 500, 1500, "T02 Deep link (clean URL /pitches)"], "isController": true}, {"data": [1.0, 500, 1500, "GET offline.html (service worker precache)"], "isController": false}, {"data": [1.0, 500, 1500, "GET manifest.json"], "isController": false}, {"data": [1.0, 500, 1500, "GET /pitches (404.html redirect page)"], "isController": false}, {"data": [0.9996763754045307, 500, 1500, "T05 Home page - reload (warm cache)"], "isController": true}, {"data": [1.0, 500, 1500, "GET robots.txt"], "isController": false}, {"data": [0.9980632666236281, 500, 1500, "GET / (index.html + all css/js/fonts)"], "isController": false}, {"data": [1.0, 500, 1500, "T04 SEO files (crawler)"], "isController": true}, {"data": [1.0, 500, 1500, "GET sw.js"], "isController": false}, {"data": [1.0, 500, 1500, "GET icons/icon-192.png"], "isController": false}, {"data": [1.0, 500, 1500, "T03 Shop - 3D shirt model"], "isController": true}]}, function(index, item){
        switch(index){
            case 0:
                item = item.toFixed(3);
                break;
            case 1:
            case 2:
                item = formatDuration(item);
                break;
        }
        return item;
    }, [[0, 0]], 3);

    // Create statistics table
    createTable($("#statisticsTable"), {"supportsControllersDiscrimination": true, "overall": {"data": ["Total", 32155, 5, 0.015549681231534754, 8.28287980096402, 0, 1196, 1.0, 34.0, 52.0, 100.9900000000016, 107.11763451749594, 20835.35149548381, 1108.1967090132634], "isController": false}, "titles": ["Label", "#Samples", "FAIL", "Error %", "Average", "Min", "Max", "Median", "90th pct", "95th pct", "99th pct", "Transactions/s", "Received", "Sent"], "items": [{"data": ["T01 Home page - full load", 3098, 5, 0.16139444803098774, 60.22627501613939, 26, 1204, 48.0, 98.09999999999991, 129.0, 218.0, 10.32078381988933, 13774.630996139493, 673.0263196592525], "isController": true}, {"data": ["GET models/tee.glb", 3018, 0, 0.0, 2.0941020543406235, 0, 90, 2.0, 3.0, 4.0, 16.0, 10.284055284464193, 5719.30058925456, 2.952648685187962], "isController": false}, {"data": ["GET og-image.png", 3018, 0, 0.0, 0.5218687872763436, 0, 29, 0.0, 1.0, 1.0, 3.0, 10.28419546105091, 1050.8559647183604, 2.9326026119402986], "isController": false}, {"data": ["GET sitemap.xml", 3018, 0, 0.0, 0.4748177601060297, 0, 8, 0.0, 1.0, 1.0, 3.0, 10.28416041654598, 14.492229961988134, 2.9225494933739062], "isController": false}, {"data": ["GET / reload (If-None-Match on every file)", 1545, 0, 0.0, 42.507443365695835, 17, 1037, 33.0, 73.0, 90.69999999999982, 167.15999999999985, 5.149313424876683, 416.46687993684174, 421.09787054434406], "isController": false}, {"data": ["T02 Deep link (clean URL /pitches)", 3048, 0, 0.0, 0.8293963254593169, 0, 83, 1.0, 1.0, 2.0, 4.0, 10.293750126645547, 6.674853597746723, 2.885064732761008], "isController": true}, {"data": ["GET offline.html (service worker precache)", 3098, 0, 0.0, 0.39541639767592013, 0, 31, 0.0, 1.0, 1.0, 2.0, 10.365745527304606, 14.212408906577798, 2.9558571230204533], "isController": false}, {"data": ["GET manifest.json", 3098, 0, 0.0, 0.8192382182052952, 0, 20, 0.0, 2.0, 3.0, 7.0, 10.365572114001218, 7.419887069885637, 2.965930302150739], "isController": false}, {"data": ["GET /pitches (404.html redirect page)", 3048, 0, 0.0, 0.8290682414698161, 0, 83, 1.0, 1.0, 2.0, 4.0, 10.293784890966258, 6.674876140235933, 2.885074476276676], "isController": false}, {"data": ["T05 Home page - reload (warm cache)", 1545, 0, 0.0, 42.507443365695835, 17, 1037, 33.0, 73.0, 90.69999999999982, 167.15999999999985, 5.14927910092587, 416.46410387962356, 421.0950636181768], "isController": true}, {"data": ["GET robots.txt", 3018, 0, 0.0, 0.7375745526838977, 0, 18, 1.0, 1.0, 2.0, 6.0, 10.28416041654598, 5.8450989867478125, 2.9125063679671235], "isController": false}, {"data": ["GET / (index.html + all css/js/fonts)", 3098, 5, 0.16139444803098774, 58.17301484828927, 26, 1196, 47.0, 94.09999999999991, 125.04999999999973, 214.0, 10.323707203934871, 13644.405150080935, 661.4414772713072], "isController": false}, {"data": ["T04 SEO files (crawler)", 3018, 0, 0.0, 1.73459244532803, 0, 29, 1.0, 3.0, 5.0, 11.0, 10.284125372279886, 1071.1860625800784, 8.767618603515958], "isController": true}, {"data": ["GET sw.js", 3098, 0, 0.0, 0.4803098773402195, 0, 27, 0.0, 1.0, 1.0, 3.0, 10.365710844179743, 13.928923946866531, 2.884987881436745], "isController": false}, {"data": ["GET icons/icon-192.png", 3098, 0, 0.0, 0.35829567462879275, 0, 29, 0.0, 1.0, 1.0, 2.0, 10.36581489425062, 99.11298206016387, 3.0166141000846527], "isController": false}, {"data": ["T03 Shop - 3D shirt model", 3018, 0, 0.0, 2.0941020543406235, 0, 90, 2.0, 3.0, 4.0, 16.0, 10.284055284464193, 5719.30058925456, 2.952648685187962], "isController": true}]}, function(index, item){
        switch(index){
            // Errors pct
            case 3:
                item = item.toFixed(2) + '%';
                break;
            // Mean
            case 4:
            // Mean
            case 7:
            // Median
            case 8:
            // Percentile 1
            case 9:
            // Percentile 2
            case 10:
            // Percentile 3
            case 11:
            // Throughput
            case 12:
            // Kbytes/s
            case 13:
            // Sent Kbytes/s
                item = item.toFixed(2);
                break;
        }
        return item;
    }, [[0, 0]], 0, summaryTableHeader);

    // Create error table
    createTable($("#errorsTable"), {"supportsControllersDiscrimination": false, "titles": ["Type of error", "Number of errors", "% in errors", "% in all samples"], "items": [{"data": ["Assertion failed", 5, 100.0, 0.015549681231534754], "isController": false}]}, function(index, item){
        switch(index){
            case 2:
            case 3:
                item = item.toFixed(2) + '%';
                break;
        }
        return item;
    }, [[1, 1]]);

        // Create top5 errors by sampler
    createTable($("#top5ErrorsBySamplerTable"), {"supportsControllersDiscrimination": false, "overall": {"data": ["Total", 32155, 5, "Assertion failed", 5, "", "", "", "", "", "", "", ""], "isController": false}, "titles": ["Sample", "#Samples", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors"], "items": [{"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": ["GET / (index.html + all css/js/fonts)", 3098, 5, "Assertion failed", 5, "", "", "", "", "", "", "", ""], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}]}, function(index, item){
        return item;
    }, [[0, 0]], 0);

});
