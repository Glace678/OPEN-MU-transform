param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$generatorProject = Join-Path $repositoryRoot 'src\Persistence\SourceGenerator\MUnique.OpenMU.Persistence.SourceGenerator.csproj'
$generatorAssembly = Join-Path $repositoryRoot "src\Persistence\SourceGenerator\bin\$Configuration\MUnique.OpenMU.Persistence.SourceGenerator.dll"
$basicModelDirectory = Join-Path $repositoryRoot 'src\Persistence\BasicModel'
$entityFrameworkModelDirectory = Join-Path $repositoryRoot 'src\Persistence\EntityFramework\Model'

# Keep source generation outside the normal project graph. The generator references
# DataModel, so a nested build from Persistence would re-enter an active build graph.
dotnet build $generatorProject `
    -c $Configuration --disable-build-servers -m:1 --no-incremental `
    -p:BuildInParallel=false -p:UseSharedCompilation=false `
    -p:GeneratePersistenceModels=false -p:PersistenceGeneratorRunning=true
if ($LASTEXITCODE -ne 0) {
    throw 'Persistence model generator build failed.'
}

if (-not (Test-Path -LiteralPath $generatorAssembly -PathType Leaf)) {
    throw "Persistence model generator output is missing: $generatorAssembly"
}

dotnet exec $generatorAssembly 'MUnique.OpenMU.Persistence' $basicModelDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'Basic persistence model generation failed.'
}

dotnet exec $generatorAssembly 'MUnique.OpenMU.Persistence.EntityFramework' $entityFrameworkModelDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'Entity Framework persistence model generation failed.'
}

Write-Output 'Persistence models regenerated successfully.'
