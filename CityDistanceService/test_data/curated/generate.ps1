# Generator for curated test data.
#
# Produces 5 CSV files (en, fr, ja, ru, ar) under this directory, each holding
# 100 cities with matching wikidata IDs so that every city has 5 language
# variants. The dataset is deterministic.
#
# Run from a PowerShell 7+ session:
#     pwsh ./generate.ps1

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
if ([string]::IsNullOrEmpty($here)) { $here = (Get-Location).Path }

# ----------------------------------------------------------------------------
# Anchor cities: well-known cities with hand-crafted multilingual names.
# These power the search assertions in SuggestionsSearchIntegrationTests.
# ----------------------------------------------------------------------------
$anchors = @(
    [ordered]@{ id='QTEST_TOKYO';    cc='JP'; lat=35.6895;  lon=139.6917; pop=14264798; en='Tokyo';        fr='Tokyo';         ja='東京';            ru='Токио';        ar='طوكيو';        country_en='Japan';        admin_en='Tokyo' },
    [ordered]@{ id='QTEST_LONDON';   cc='GB'; lat=51.5074;  lon=-0.1278;  pop=9648110;  en='London';       fr='Londres';       ja='ロンドン';        ru='Лондон';       ar='لندن';         country_en='United Kingdom'; admin_en='Greater London' },
    [ordered]@{ id='QTEST_PARIS';    cc='FR'; lat=48.8566;  lon=2.3522;   pop=2148000;  en='Paris';        fr='Paris';         ja='パリ';            ru='Париж';        ar='باريس';        country_en='France';       admin_en='Île-de-France' },
    [ordered]@{ id='QTEST_MOSCOW';   cc='RU'; lat=55.7558;  lon=37.6173;  pop=12655050; en='Moscow';       fr='Moscou';        ja='モスクワ';        ru='Москва';       ar='موسكو';        country_en='Russia';       admin_en='Moscow' },
    [ordered]@{ id='QTEST_CAIRO';    cc='EG'; lat=30.0444;  lon=31.2357;  pop=10100166; en='Cairo';        fr='Le Caire';      ja='カイロ';          ru='Каир';         ar='القاهرة';      country_en='Egypt';        admin_en='Cairo Governorate' },
    [ordered]@{ id='QTEST_BERLIN';   cc='DE'; lat=52.52;    lon=13.405;   pop=3769495;  en='Berlin';       fr='Berlin';        ja='ベルリン';        ru='Берлин';       ar='برلين';        country_en='Germany';      admin_en='Berlin' },
    [ordered]@{ id='QTEST_ROME';     cc='IT'; lat=41.9028;  lon=12.4964;  pop=2761000;  en='Rome';         fr='Rome';          ja='ローマ';          ru='Рим';          ar='روما';         country_en='Italy';        admin_en='Lazio' },
    [ordered]@{ id='QTEST_MADRID';   cc='ES'; lat=40.4168;  lon=-3.7038;  pop=3223000;  en='Madrid';       fr='Madrid';        ja='マドリード';      ru='Мадрид';       ar='مدريد';        country_en='Spain';        admin_en='Madrid' },
    [ordered]@{ id='QTEST_BEIJING';  cc='CN'; lat=39.9042;  lon=116.4074; pop=21893000; en='Beijing';      fr='Pékin';         ja='北京';            ru='Пекин';        ar='بكين';         country_en='China';        admin_en='Beijing' },
    [ordered]@{ id='QTEST_MUMBAI';   cc='IN'; lat=19.0760;  lon=72.8777;  pop=20411000; en='Mumbai';       fr='Bombay';        ja='ムンバイ';        ru='Мумбаи';       ar='مومباي';       country_en='India';        admin_en='Maharashtra' },
    [ordered]@{ id='QTEST_NEWYORK';  cc='US'; lat=40.7128;  lon=-74.0060; pop=8336000;  en='New York';     fr='New York';      ja='ニューヨーク';    ru='Нью-Йорк';     ar='نيويورك';      country_en='United States';admin_en='New York' },
    [ordered]@{ id='QTEST_SYDNEY';   cc='AU'; lat=-33.8688; lon=151.2093; pop=5312000;  en='Sydney';       fr='Sydney';        ja='シドニー';        ru='Сидней';       ar='سيدني';        country_en='Australia';    admin_en='New South Wales' },
    [ordered]@{ id='QTEST_ISTANBUL'; cc='TR'; lat=41.0082;  lon=28.9784;  pop=15462000; en='Istanbul';     fr='Istanbul';      ja='イスタンブール';  ru='Стамбул';      ar='إسطنبول';      country_en='Turkey';       admin_en='Istanbul' },
    [ordered]@{ id='QTEST_TORONTO';  cc='CA'; lat=43.6532;  lon=-79.3832; pop=2930000;  en='Toronto';      fr='Toronto';       ja='トロント';        ru='Торонто';      ar='تورونتو';      country_en='Canada';       admin_en='Ontario' },
    [ordered]@{ id='QTEST_LA';       cc='US'; lat=34.0522;  lon=-118.2437;pop=3979000;  en='Los Angeles';  fr='Los Angeles';   ja='ロサンゼルス';    ru='Лос-Анджелес'; ar='لوس أنجلوس';   country_en='United States';admin_en='California' },
    [ordered]@{ id='QTEST_SANFRAN';  cc='US'; lat=37.7749;  lon=-122.4194;pop=873965;   en='San Francisco';fr='San Francisco'; ja='サンフランシスコ';ru='Сан-Франциско';ar='سان فرانسيسكو';country_en='United States';admin_en='California' },
    [ordered]@{ id='QTEST_BARCELONA';cc='ES'; lat=41.3851;  lon=2.1734;   pop=1620000;  en='Barcelona';    fr='Barcelone';     ja='バルセロナ';      ru='Барселона';    ar='برشلونة';      country_en='Spain';        admin_en='Catalonia' },
    [ordered]@{ id='QTEST_VIENNA';   cc='AT'; lat=48.2082;  lon=16.3738;  pop=1911000;  en='Vienna';       fr='Vienne';        ja='ウィーン';        ru='Вена';         ar='فيينا';        country_en='Austria';      admin_en='Vienna' },
    [ordered]@{ id='QTEST_PRAGUE';   cc='CZ'; lat=50.0755;  lon=14.4378;  pop=1309000;  en='Prague';       fr='Prague';        ja='プラハ';          ru='Прага';        ar='براغ';         country_en='Czech Republic';admin_en='Prague' },
    [ordered]@{ id='QTEST_WARSAW';   cc='PL'; lat=52.2297;  lon=21.0122;  pop=1790658;  en='Warsaw';       fr='Varsovie';      ja='ワルシャワ';      ru='Варшава';      ar='وارسو';        country_en='Poland';       admin_en='Masovia' },
    [ordered]@{ id='QTEST_BUDAPEST'; cc='HU'; lat=47.4979;  lon=19.0402;  pop=1752000;  en='Budapest';     fr='Budapest';      ja='ブダペスト';      ru='Будапешт';     ar='بودابست';      country_en='Hungary';      admin_en='Budapest' },
    [ordered]@{ id='QTEST_ATHENS';   cc='GR'; lat=37.9838;  lon=23.7275;  pop=664046;   en='Athens';       fr='Athènes';       ja='アテネ';          ru='Афины';        ar='أثينا';        country_en='Greece';       admin_en='Attica' },
    [ordered]@{ id='QTEST_LISBON';   cc='PT'; lat=38.7223;  lon=-9.1393;  pop=505526;   en='Lisbon';       fr='Lisbonne';      ja='リスボン';        ru='Лиссабон';     ar='لشبونة';       country_en='Portugal';     admin_en='Lisbon' },
    [ordered]@{ id='QTEST_DUBLIN';   cc='IE'; lat=53.3498;  lon=-6.2603;  pop=554554;   en='Dublin';       fr='Dublin';        ja='ダブリン';        ru='Дублин';       ar='دبلن';         country_en='Ireland';      admin_en='Leinster' },
    [ordered]@{ id='QTEST_AMSTERDAM';cc='NL'; lat=52.3676;  lon=4.9041;   pop=872680;   en='Amsterdam';    fr='Amsterdam';     ja='アムステルダム';  ru='Амстердам';    ar='أمستردام';     country_en='Netherlands';  admin_en='North Holland' },

    # --- "Competitor" cities that share prefixes with the anchor cities so
    #     they can collide in the fuzzy expansion budget. ----------------------
    [ordered]@{ id='QTEST_TOBA';     cc='JP'; lat=34.4823;  lon=136.8456; pop=18120;    en='Toba';         fr='Toba';          ja='鳥羽';            ru='Тоба';         ar='توبا';         country_en='Japan';        admin_en='Mie Prefecture' },
    [ordered]@{ id='QTEST_TOKAT';    cc='TR'; lat=40.3167;  lon=36.55;    pop=145753;   en='Tokat';        fr='Tokat';         ja='トカット';        ru='Токат';        ar='توقات';        country_en='Turkey';       admin_en='Tokat Province' },
    [ordered]@{ id='QTEST_TOKMAK';   cc='UA'; lat=47.2547;  lon=35.7142;  pop=30358;    en='Tokmak';       fr='Tokmak';        ja='トクマク';        ru='Токмак';       ar='توكماك';       country_en='Ukraine';      admin_en='Zaporizhzhia Oblast' },
    [ordered]@{ id='QTEST_TOLEDO';   cc='ES'; lat=39.8628;  lon=-4.0273;  pop=83741;    en='Toledo';       fr='Tolède';        ja='トレド';          ru='Толедо';       ar='طليطلة';       country_en='Spain';        admin_en='Castilla–La Mancha' },
    [ordered]@{ id='QTEST_TOMSK';    cc='RU'; lat=56.4977;  lon=84.9744;  pop=576624;   en='Tomsk';        fr='Tomsk';         ja='トムスク';        ru='Томск';        ar='تومسك';        country_en='Russia';       admin_en='Tomsk Oblast' },
    [ordered]@{ id='QTEST_TOBOLSK';  cc='RU'; lat=58.1992;  lon=68.2542;  pop=98772;    en='Tobolsk';      fr='Tobolsk';       ja='トボリスク';      ru='Тобольск';     ar='توبولسك';      country_en='Russia';       admin_en='Tyumen Oblast' },
    [ordered]@{ id='QTEST_TONGLING'; cc='CN'; lat=30.9450;  lon=117.8121; pop=1605000;  en='Tongling';     fr='Tongling';      ja='銅陵';            ru='Тунлин';       ar='تونغلينغ';     country_en='China';        admin_en='Anhui' },
    [ordered]@{ id='QTEST_TOPEKA';   cc='US'; lat=39.0473;  lon=-95.6752; pop=125310;   en='Topeka';       fr='Topeka';        ja='トピカ';          ru='Топика';       ar='توبيكا';       country_en='United States';admin_en='Kansas' },
    [ordered]@{ id='QTEST_TONGEREN'; cc='BE'; lat=50.7806;  lon=5.4647;   pop=30789;    en='Tongeren';     fr='Tongres';       ja='トンゲレン';      ru='Тонгерен';     ar='تونغيرين';     country_en='Belgium';      admin_en='Limburg' },
    [ordered]@{ id='QTEST_TONGCHENG';cc='CN'; lat=31.0517;  lon=116.95;   pop=750000;   en='Tongcheng';    fr='Tongcheng';     ja='桐城';            ru='Тунчэн';       ar='تونغتشنغ';     country_en='China';        admin_en='Anhui' }
)

# ----------------------------------------------------------------------------
# Synthetic noise cities. Names are deterministic. They share many common
# prefixes (Sa*, Ma*, Ka*, Ta*, To*, etc.) with the anchor cities to ensure
# the index has a realistic term-dictionary density per prefix bucket.
# ----------------------------------------------------------------------------
$rootList = @(
    'Saburn','Salinor','Sandiri','Saporo','Sarnov','Savina','Selton','Senova','Serenia','Shorbach',
    'Silvar','Skalden','Solveig','Storfen','Sumika','Sungavi','Suzaka','Sverda','Talburn','Tamora',
    'Tarnia','Tavira','Telvin','Tenova','Terburg','Tessar','Tilmore','Tindor','Tirashi','Toburg',
    'Tomanto','Torindel','Tovasi','Tovira','Tremola','Trinova','Tukana','Tulvan','Tuverno','Tyresso',
    'Ubaldi','Uleren','Umberto','Undara','Uravna','Urlanga','Uvalde','Vakarno','Valborg','Vandrim',
    'Vasilev','Veldarn','Verkala','Vesmara','Vidura','Vilnara','Vinarov','Vintora','Voldura','Vrenta',
    'Walmont','Waterka','Wenrod','Weshira','Wickbury','Wintora','Worsten','Xanorra','Xerdana','Yalvora',
    'Yarvin','Yedina','Yorenta'
)
if ($rootList.Count -lt 65) { throw "Need at least 65 root names, got $($rootList.Count)" }

$synthetic = @()
for ($i = 0; $i -lt 65; $i++) {
    $name = $rootList[$i]
    $synthetic += [ordered]@{
        id          = "QTEST_SYN$([string]::Format('{0:D3}', $i+1))"
        cc          = ''
        lat         = [Math]::Round(-60 + ($i * 1.7), 4)
        lon         = [Math]::Round(-179 + ($i * 4.9), 4)
        pop         = (1000 + ($i * 137))
        en          = $name
        fr          = $name
        ja          = $name
        ru          = $name
        ar          = $name
        country_en  = ''
        admin_en    = ''
    }
}

$cities = @($anchors) + @($synthetic)
if ($cities.Count -ne 100) { throw "Expected exactly 100 cities, got $($cities.Count)" }

function Escape-Csv($v) {
    if ($null -eq $v) { return '' }
    $s = [string]$v
    if ($s -match '[",\r\n]') {
        return '"' + ($s -replace '"','""') + '"'
    }
    return $s
}

function Write-Lang($lang) {
    $lines = @('city_id,city_name,language,latitude,longitude,country,country_code,admin_region,population')
    foreach ($c in $cities) {
        $name = $c[$lang]
        if ([string]::IsNullOrEmpty($name)) { continue }
        $country = $c['country_en']
        $admin   = $c['admin_en']
        $cols = @(
            (Escape-Csv $c['id'])
            (Escape-Csv $name)
            (Escape-Csv $lang)
            (Escape-Csv $c['lat'])
            (Escape-Csv $c['lon'])
            (Escape-Csv $country)
            (Escape-Csv $c['cc'])
            (Escape-Csv $admin)
            (Escape-Csv $c['pop'])
        )
        $lines += ($cols -join ',')
    }
    $path = Join-Path $here "${lang}_cities.csv"
    # Write UTF-8 without BOM so CsvHelper reads cleanly
    [System.IO.File]::WriteAllLines($path, $lines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Wrote $path ($(($lines.Count - 1)) rows)"
}

foreach ($lang in @('en','fr','ja','ru','ar')) { Write-Lang $lang }
