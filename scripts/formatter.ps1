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
        Write-Warning "Invalid JSON detected, skipping."
        return $null
    }

    function _stringify {
        param(
            $value,
            [string]$currentIndent = ""
        )

        # Handle primitives
        if ($value -is [string]) {
            return '"' + ($value -replace '"','\"') + '"'
        }
        elseif ($value -is [bool]) {
            return $value.ToString().ToLower()
        }
        elseif ($value -is [int] -or $value -is [double] -or $value -eq $null) {
            if ($value -eq $null) { return "null" }
            else { return $value.ToString() }
        }


        # Handle arrays
        elseif ($value -is [System.Array] -or $value -is [System.Collections.IList]) {
            $nextIndent = $currentIndent + (" " * $Indent)
            $items = @()
            foreach ($item in $value) {
                $items += _stringify $item $nextIndent
            }

            # Try inline if short
            $inline = "[" + ($items -join ", ") + "]"
            if ($inline.Length + $currentIndent.Length -le $MaxLength) { return $inline }

            return "[`n$nextIndent$($items -join ",`n$nextIndent")`n$currentIndent]"
        }

        # Handle objects
        elseif ($value -is [PSCustomObject] -or $value -is [Hashtable]) {
            $nextIndent = $currentIndent + (" " * $Indent)
            $items = @()
            foreach ($prop in $value.PSObject.Properties) {
                $key = $prop.Name
                $val = _stringify $prop.Value $nextIndent
                $items += '"' + $key + '": ' + $val
            }

            # Try inline if short
            $inline = "{ " + ($items -join ", ") + " }"
            if ($inline.Length + $currentIndent.Length -le $MaxLength) { return $inline }

            return "{`n$nextIndent$($items -join ",`n$nextIndent")`n$currentIndent}"
        }

        # fallback
        return '"' + ($value.ToString() -replace '"','\"') + '"'
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
        if (Test-Path $temp) { Remove-Item $temp -Force -ErrorAction SilentlyContinue }
    }
}