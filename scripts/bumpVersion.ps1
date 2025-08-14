function UpdateVersionAndReleaseNotes {
    param (
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$ReleaseNotesResult,

        [Parameter(Mandatory=$true)]
        [string]$XmlFilePath
    )

    # Load XML
    $xmlContent = New-Object XML
    $xmlContent.Load($XmlFilePath)

    # Find the first PropertyGroup
    $propertyGroup = $xmlContent.SelectSingleNode("//PropertyGroup[1]")
    
    if (-not $propertyGroup) {
        throw "No PropertyGroup found in $XmlFilePath"
    }

    # Update or create VersionPrefix
    $versionPrefixElement = $xmlContent.SelectSingleNode("//VersionPrefix")
    if ($versionPrefixElement) {
        $versionPrefixElement.InnerText = $ReleaseNotesResult.Version
    } else {
        $versionPrefixElement = $xmlContent.CreateElement("VersionPrefix")
        $versionPrefixElement.InnerText = $ReleaseNotesResult.Version
        $propertyGroup.PrependChild($versionPrefixElement) | Out-Null
    }

    # Update or create PackageReleaseNotes
    $packageReleaseNotesElement = $xmlContent.SelectSingleNode("//PackageReleaseNotes")
    if ($packageReleaseNotesElement) {
        $packageReleaseNotesElement.InnerText = $ReleaseNotesResult.ReleaseNotes
    } else {
        $packageReleaseNotesElement = $xmlContent.CreateElement("PackageReleaseNotes")
        $packageReleaseNotesElement.InnerText = $ReleaseNotesResult.ReleaseNotes
        $propertyGroup.AppendChild($packageReleaseNotesElement) | Out-Null
    }

    # Save the updated XML with proper formatting
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = "`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
    
    $writer = [System.Xml.XmlWriter]::Create($XmlFilePath, $settings)
    $xmlContent.Save($writer)
    $writer.Close()
}

# Usage example:
# $notes = Get-ReleaseNotes -MarkdownFile "$PSScriptRoot\RELEASE_NOTES.md"
# $propsPath = Join-Path -Path (Get-Item $PSScriptRoot).Parent.FullName -ChildPath "Directory.Build.props"
# UpdateVersionAndReleaseNotes -ReleaseNotesResult $notes -XmlFilePath $propsPath
