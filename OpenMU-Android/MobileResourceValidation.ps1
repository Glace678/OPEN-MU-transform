function Get-MobileLocaleFolder {
    param([string]$Locale)

    switch ($Locale) {
        'zh-CN' { return 'values-zh-rCN' }
        'zh-TW' { return 'values-zh-rTW' }
        'id' { return 'values-b+id' }
        'in' { return 'values-b+id' }
        'tl' { return 'values-b+tl' }
        'fil' { return 'values-b+fil' }
        default { return "values-$Locale" }
    }
}

function Add-MobileResourceEntries {
    param([xml]$Document, $Catalog, [string]$Context)

    foreach ($entry in $Document.resources.ChildNodes) {
        if ($entry.LocalName -notin @('string', 'string-array')) { continue }
        $name = $entry.GetAttribute('name')
        if ([string]::IsNullOrWhiteSpace($name) -or $Catalog.ContainsKey($name)) {
            throw "Missing or duplicate resource name in ${Context}: $name"
        }
        if ($entry.GetAttribute('translatable') -eq 'false' -or $entry.GetAttribute('formatted') -eq 'false') {
            throw "Resource validation is disabled in ${Context}: $name"
        }
        $Catalog.Add($name, $entry)
    }
}

function Read-MobileResourceCatalog {
    param([string]$Directory)

    $catalog = [Collections.Generic.Dictionary[string, Xml.XmlElement]]::new([StringComparer]::Ordinal)
    $files = @(Get-ChildItem -LiteralPath $Directory -Filter '*.xml' -File)
    if ($files.Count -eq 0) { throw "No XML resources in $Directory" }
    foreach ($file in $files) {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw
        Add-MobileResourceEntries $document $catalog $file.FullName
    }
    return ,$catalog
}

function Test-MobileResourceText {
    param([string]$Reference, [string]$Localized, [string]$Context)

    if ([string]::IsNullOrWhiteSpace($Localized)) { throw "Empty resource in $Context" }
    $formatPattern = '%(?:\d+\$)?[-#+ 0,(<]*\d*(?:\.\d+)?[a-zA-Z%]'
    $expected = @([regex]::Matches($Reference, $formatPattern) | ForEach-Object Value | Sort-Object) -join '|'
    $actual = @([regex]::Matches($Localized, $formatPattern) | ForEach-Object Value | Sort-Object) -join '|'
    if ($expected -cne $actual -or [regex]::Replace($Localized, $formatPattern, '').Contains('%')) {
        throw "Format placeholder mismatch in $Context"
    }
    if ([regex]::IsMatch($Localized, '(?<!\\)(?:\\\\)*[''"]')) {
        throw "Unescaped Android quote in $Context"
    }
    if ([regex]::IsMatch($Localized, '\\(?![ntr\\''"@?u])')) {
        throw "Invalid Android escape in $Context"
    }
    if ([regex]::Matches($Reference, '\\n').Count -ne [regex]::Matches($Localized, '\\n').Count) {
        throw "Line break mismatch in $Context"
    }
}

function Test-MobileResourceEntry {
    param([Xml.XmlElement]$Reference, [Xml.XmlElement]$Localized, [string]$Context)

    if ($Reference.LocalName -cne $Localized.LocalName) { throw "Resource type mismatch in $Context" }
    if ($Reference.LocalName -eq 'string') {
        Test-MobileResourceText $Reference.InnerText $Localized.InnerText $Context
        return
    }
    $expected = @($Reference.SelectNodes('item'))
    $actual = @($Localized.SelectNodes('item'))
    if ($expected.Count -ne $actual.Count) { throw "Array length mismatch in $Context" }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        Test-MobileResourceText $expected[$index].InnerText $actual[$index].InnerText "$Context/$index"
    }
}

function Test-MobileResourceCatalog {
    param($Reference, $Localized, [string]$Context)

    if ($Reference.Count -ne $Localized.Count) { throw "Resource key count mismatch in $Context" }
    foreach ($name in $Reference.Keys) {
        if (!$Localized.ContainsKey($name)) { throw "Missing resource in ${Context}: $name" }
        Test-MobileResourceEntry $Reference[$name] $Localized[$name] "$Context/$name"
    }
}
