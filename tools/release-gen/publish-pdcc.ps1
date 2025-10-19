param($os, $arch)

$ErrorActionPreference = "Stop"

$PUBLISH_TARGET = "..\out\pdcc"

if ($(Test-Path ./out) -eq $false) {
    mkdir out
} else {
    rm out/* -Recurse -Force
}
#dotnet clean


Write-Host "Publish parameters: OS=$os, Platform=$arch"

if (($os -eq "any" ) -and ($arch -eq "any")) {
    $runtimeIdentifier = "-p:RuntimeIdentifier="
} else {
    $runtimeIdentifier = "-p:RuntimeIdentifier=$os-$arch"
}

Write-Host "Building PDCC..." -ForegroundColor Cyan
dotnet publish ./PhainonDistributionCenter.Client/PhainonDistributionCenter.Client.csproj -c Release -p:PublishDir=$PUBLISH_TARGET $runtimeIdentifier -p:PublishSingleFile=true

Write-Host "Packaging..." -ForegroundColor Cyan

7z a ./out/${env:artifact_name}.zip ./out/pdcc/* -r

Write-Host "Successfully published to $PUBLISH_TARGET" -ForegroundColor Green