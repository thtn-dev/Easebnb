<#
.SYNOPSIS
Creates a new module for the Modular Monolith.

.DESCRIPTION
Creates a module under the Modules directory with three projects:

- {ModuleName}.Domain
- {ModuleName}.Application
- {ModuleName}.Infrastructure

The script automatically:
- Creates the module folder.
- Creates the three class library projects.
- Adds project references:
    Application -> Domain
    Infrastructure -> Application
    Infrastructure -> Domain
- Optionally references BuildingBlocks projects.
- Adds all projects to the root .slnx solution.

.PARAMETER ModuleName
Name of the module.

.PARAMETER NoBuildingBlocks
Skip adding references to BuildingBlocks projects.

.EXAMPLE
.\scripts\new-module.ps1 -ModuleName Identity

Creates:

Modules/
└── Identity
    ├── Identity.Domain
    ├── Identity.Application
    └── Identity.Infrastructure

Adds BuildingBlocks references and adds all projects to the root .slnx.

.EXAMPLE
.\scripts\new-module.ps1 -ModuleName Identity -NoBuildingBlocks

Creates the same module structure without referencing any BuildingBlocks projects.

.NOTES
Expected repository layout:

/
├── BuildingBlocks
│   ├── BuildingBlocks.Application
│   ├── BuildingBlocks.Infrastructure
│   └── BuildingBlocks.SharedKernel
├── Modules
├── scripts
└── *.slnx

#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleName,

    [string]$BaseNamespace = "TmsBase",

    # Disable tất cả BuildingBlocks reference
    [switch]$NoBuildingBlocks,

    [switch]$SkipSolution
)

$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
$ModulesRoot = Join-Path $Root "Modules"
$ModuleRoot = Join-Path $ModulesRoot $ModuleName
$ProjectPrefix = "$BaseNamespace.$ModuleName"

if (Test-Path $ModuleRoot) {
    Write-Host ""
    Write-Host "ERROR: Module '$ModuleName' already exists." -ForegroundColor Red
    exit 1
}

Write-Host "Creating module $ModuleName..."

New-Item -ItemType Directory -Path $ModuleRoot | Out-Null

$projects = @(
    "Domain",
    "Application",
    "Infrastructure"
)

foreach ($project in $projects)
{
    $ProjectName = "$ProjectPrefix.$project"

    dotnet new classlib `
        -n $ProjectName `
        -o "$ModuleRoot/$ProjectName"
}
# ===========================
# Internal module references
# ===========================

dotnet add `
"$ModuleRoot/$ProjectPrefix.Application/$ProjectPrefix.Application.csproj" `
reference `
"$ModuleRoot/$ProjectPrefix.Domain/$ProjectPrefix.Domain.csproj"

dotnet add `
"$ModuleRoot/$ProjectPrefix.Infrastructure/$ProjectPrefix.Infrastructure.csproj" `
reference `
"$ModuleRoot/$ProjectPrefix.Domain/$ProjectPrefix.Domain.csproj"

dotnet add `
"$ModuleRoot/$ProjectPrefix.Infrastructure/$ProjectPrefix.Infrastructure.csproj" `
reference `
"$ModuleRoot/$ProjectPrefix.Application/$ProjectPrefix.Application.csproj"

# ===========================
# BuildingBlocks references
# ===========================

if (-not $NoBuildingBlocks)
{
    $BBApp = Join-Path $Root "BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj"
    $BBInfra = Join-Path $Root "BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj"
    $BBShared = Join-Path $Root "BuildingBlocks/BuildingBlocks.SharedKernel/BuildingBlocks.SharedKernel.csproj"

    # Domain
    dotnet add `
    "$ModuleRoot/$ProjectPrefix.Domain/$ProjectPrefix.Domain.csproj" `
    reference `
    $BBShared

    # Application
    dotnet add `
    "$ModuleRoot/$ProjectPrefix.Application/$ProjectPrefix.Application.csproj" `
    reference `
    $BBApp

    # Infrastructure
    dotnet add `
    "$ModuleRoot/$ProjectPrefix.Infrastructure/$ProjectPrefix.Infrastructure.csproj" `
    reference `
    $BBInfra
}

Write-Host ""
Write-Host "Module '$ModuleName' created successfully." -ForegroundColor Green

if (-not $SkipSolution)
{
    $solution = Get-ChildItem -Path $Root -Filter *.slnx -File | Select-Object -First 1

    if (-not $solution)
    {
        $solution = Get-ChildItem -Path $Root -Filter *.sln -File | Select-Object -First 1
    }

    if (-not $solution)
    {
        Write-Warning "No solution (.slnx/.sln) found. Skipping add to solution."
    }
    else
    {
        Get-ChildItem -Path $ModuleRoot -Recurse -Filter *.csproj |
            ForEach-Object {
                dotnet sln $solution.FullName add $_.FullName
            }

        Write-Host "Projects added to $($solution.Name)." -ForegroundColor Green
    }
}