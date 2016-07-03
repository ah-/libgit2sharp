<#
.SYNOPSIS
    Generates the NuGet packages (including the symbols).
    A clean build is performed the packaging.
.PARAMETER commitSha
    The LibGit2Sharp commit sha that contains the version of the source code being packaged.
#>

Param(
    [Parameter(Mandatory=$true)]
    [string]$commitSha,
    [scriptblock]$postBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Run-Command([scriptblock]$Command) {
    $output = ""

    $exitCode = 0
    $global:lastexitcode = 0

    & $Command

    if ($LastExitCode -ne 0) {
        $exitCode = $LastExitCode
    } elseif (!$?) {
        $exitCode = 1
    } else {
        return
    }

    $error = "``$Command`` failed"

    if ($output) {
        Write-Host -ForegroundColor "Red" $output
        $error += ". See output above."
    }

    Throw $error
}

function Clean-OutputFolder($folder) {

    If (Test-Path $folder) {
        Write-Host -ForegroundColor "Green" "Dropping `"$folder`" folder..."

        Run-Command { & Remove-Item -Recurse -Force "$folder" }

        Write-Host "Done."
    }
}

# From http://www.dougfinke.com/blog/index.php/2010/12/01/note-to-self-how-to-programmatically-get-the-msbuild-path-in-powershell/

Function Get-MSBuild {
    #$lib = [System.Runtime.InteropServices.RuntimeEnvironment]
    #$rtd = $lib::GetRuntimeDirectory()
    #Join-Path $rtd msbuild.exe
    "C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
}

#################

$nugetPackagePath = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$root = Join-Path $nugetPackagePath ".."
$projectPath = Join-Path $root "LibGit2Sharp"
$portableProjectPath = Join-Path $root "LibGit2Sharp.Portable"
$slnPath = Join-Path $root "LibGit2Sharp.sln"

Remove-Item (Join-Path $nugetPackagePath "*.nupkg")

Clean-OutputFolder (Join-Path $projectPath "bin\")
Clean-OutputFolder (Join-Path $projectPath "obj\")
Clean-OutputFolder (Join-Path $portableProjectPath "bin\")
Clean-OutputFolder (Join-Path $portableProjectPath "obj\")

try {
  Set-Content -Encoding ASCII $(Join-Path $root "libgit2sharp_hash.txt") $commitSha
  #Run-Command { & "$(Join-Path $root "Lib\NuGet\Nuget.exe")" Restore "$slnPath" }
  Run-Command { & (Get-MSBuild) "$slnPath" "/verbosity:minimal" "/p:Configuration=Release" }

  If ($postBuild) {
    Write-Host -ForegroundColor "Green" "Run post build script..."
    Run-Command { & ($postBuild) }
  }

  Run-Command { & "$(Join-Path $root "Lib\NuGet\Nuget.exe")" Pack -Prop Configuration=Release LibGit2Sharp.nuspec }
}
finally {
  Pop-Location
  Set-Content -Encoding ASCII $(Join-Path $root "libgit2sharp_hash.txt") "unknown"
}
