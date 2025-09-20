param(
    [Parameter(Mandatory = $true)]
    [string]$Path,          # Folder or file to format

    [int]$Indent = 2,       # Spaces per indent
    [int]$MaxLength = 80    # Max line length before multiline
)

function Convert-CustomJsonString {
    param(
        [string]$JsonText,
        [int]$Indent = 2,
        [int]$MaxLength = 80
    )

    try {
        $obj = ConvertFrom-Json $JsonText -ErrorAction Stop
    }
    catch {
        Write-Warning "Invalid JSON detected, skipping file."
        return $null
    }

    function _stringify {
        param(
            $value,
            [string]$currentIndent = "",
            [int]$reserved = 0
        )

        $jsonString = ConvertTo-Json $value -Depth 100 -Compress
        if ($jsonString -eq $null) { return $null }

        $length = $MaxLength - $currentIndent.Length - $reserved

        # Attempt short inline formatting
        if ($jsonString.Length -le $length) {
            function Add-SpaceAfterPunctuation($json) {
                $inString = $false
                $result = ""
                for ($i = 0; $i -lt $json.Length; $i++) {
                    $c = $json[$i]
                    if ($c -eq '"') { $inString = -not $inString }
                    $result += $c
                    if (-not $inString -and ($c -eq ":" -or $c -eq ",")) { $result += " " }
                }
                return $result
            }
            $prettified = Add-SpaceAfterPunctuation $jsonString
            if ($prettified.Length -le $length) { return $prettified }
        }

        # Handle arrays
        if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
            $nextIndent = $currentIndent + (" " * $Indent)
            $items = @()
            $start = "["
            $end = "]"

            # Ensure we have a real array so .Count is safe
            $arr = @($value)
            for ($i = 0; $i -lt $arr.Count; $i++) {
                $reservedForItem = if ($i -lt ($arr.Count - 1)) { 1 } else { 0 }
                $items += _stringify $arr[$i] $nextIndent $reservedForItem
            }

            if ($items.Count -gt 0) {
                return "$start`n$nextIndent$($items -join ",`n$nextIndent")`n$currentIndent$end"
            } else {
                return "[]"
            }
        }

        # Handle objects
        elseif ($value -is [PSCustomObject] -or $value -is [Hashtable]) {
            $nextIndent = $currentIndent + (" " * $Indent)
            $items = @()
            $start = "{"
            $end = "}"

            $keys = $value.PSObject.Properties | ForEach-Object { $_.Name }
            for ($i = 0; $i -lt $keys.Count; $i++) {
                $key = $keys[$i]
                $keyPart = '"' + $key + '": '

                $reservedForValue = if ($i -lt ($keys.Count - 1)) { 1 + $keyPart.Length } else { $keyPart.Length }

                # Safe property access
                $propValue = $value.PSObject.Properties[$key].Value
                $val = _stringify $propValue $nextIndent $reservedForValue

                if ($val -ne $null) { $items += $keyPart + $val }
            }

            if ($items.Count -gt 0) {
                return "$start`n$nextIndent$($items -join ",`n$nextIndent")`n$currentIndent$end"
            } else {
                return "{}"
            }
        }

        # fallback
        return $jsonString
    }

    return _stringify $obj
}

# Determine files
if (Test-Path $Path -PathType Container) {
    $files = Get-ChildItem $Path -Filter *.json -Recurse
}
elseif (Test-Path $Path -PathType Leaf) {
    $files = @(Get-Item $Path)
}
else {
    Write-Error "Path not found: $Path"
    exit 0
}

foreach ($file in $files) {
    $jsonText = Get-Content $file.FullName -Raw
    $formatted = Convert-CustomJsonString -JsonText $jsonText -Indent $Indent -MaxLength $MaxLength

    if ($formatted -eq $null) {
        Write-Warning "Skipping $($file.FullName) (invalid JSON)."
        continue
    }

    $temp = "$($file.FullName).tmp"

    try {
        Set-Content $temp $formatted -Encoding UTF8
        Move-Item -Force $temp $file.FullName
        Write-Host "Formatted $($file.FullName)"
    }
    finally {
        if (Test-Path $temp) {
            Remove-Item $temp -Force -ErrorAction SilentlyContinue
        }
    }
}
