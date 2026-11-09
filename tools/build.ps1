param([string]$Out='build/NewEra-MemorySelect.exe')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
$compiler=(Join-Path $env:WINDIR "Microsoft.NET/Framework/v4.0.30319/csc.exe")
$buildArgs=@('/nologo','/target:winexe','/platform:x86','/optimize+','/utf8output',"/out:$Out",'/main:MemorySelectV3Form','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/win32icon:assets/Rikimaru.ico','/resource:assets/Rikimaru.ico,Silkroad.ico','/resource:data/monster-names.tsv,monster-names.tsv','/resource:assets/Explorer-Harita-Sade.jpg,explorer-52.jpg','/resource:assets/Dunya-Haritasi.jpg,world-map.jpg','/resource:data/grim-locations.json,grim-locations.json','/resource:data/town-regions.json,town-regions.json','/resource:data/level-exp.json,level-exp.json','/resource:data/updates-public-key.xml,updates-public-key.xml')
1..18 | ForEach-Object {
 $trialCode='D{0:D2}' -f $_
 $trialFile='data/Grim-Haritalari/'+$trialCode+'-Solo-Trial/'+$trialCode+'-Solo-Trial.json'
 if(!(Test-Path -LiteralPath $trialFile)){throw ('Missing captured Trial map: '+$trialCode)}
 $buildArgs+='/resource:'+$trialFile+',GrimFloor.'+$trialCode+'.SoloTrial.json'
}
Get-ChildItem -LiteralPath data/grim-embedded -Filter '*.jpg' | ForEach-Object {$buildArgs+='/resource:'+$_.FullName+',Grim.'+$_.Name}
$buildArgs+=@('/resource:data/road-mesh.bin.gz,road-mesh.bin.gz','/resource:data/transport-network.json,transport-network.json','/resource:data/return-scrolls.json,return-scrolls.json')
$buildArgs+=@('/resource:assets/NewEra-Kullanim-Kilavuzu.pdf,manual.pdf','/resource:data/Nanzhang/Nanzhang-NPC-Haritasi.jpg,nanzhang-map.jpg','/resource:data/Nanzhang/Nanzhang-Harita-Verisi.json,nanzhang-data.json')
$buildArgs+=@('src\MemoryCoreV3.cs','src\RulesV2.cs','src\AutomationV3.cs','src\V3Ui.cs','src\V3App.cs','src\DungeonRadar.cs','src\GrimRooms.cs','src\FloorNavigation.cs','src\ObstacleNavigation.cs','src\RunSettings.cs','src\Hotkeys.cs','src\AutoUpdate.cs','src\AssemblyInfo.cs','src\DualAccount.cs','src\ModernUi.cs','src\Expedition.cs','src\CompletionNotice.cs','src\MountTravel.cs','src\LogView.cs','src\RoadMesh.cs','src\PhBotRoutes.cs','src\WorldMap.cs','src\GrimQueue.cs','src\CharacterStats.cs')
& $compiler @buildArgs
if($LASTEXITCODE -ne 0){throw 'V3 build failed'}
Write-Output 'V3 build OK'



