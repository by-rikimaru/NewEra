$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$compiler=(Join-Path $env:WINDIR "Microsoft.NET/Framework/v4.0.30319/csc.exe")
$buildArgs=@('/nologo','/target:exe','/platform:x86','/optimize+','/utf8output','/out:work/V3Checks.exe','/main:V3Checks','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/win32icon:outputs/Rikimaru.ico','/resource:outputs/Rikimaru.ico,Silkroad.ico','/resource:work/monster-names.tsv,monster-names.tsv','/resource:outputs/Explorer-Harita-Sade.jpg,explorer-52.jpg','/resource:outputs/v3/grim-locations.json,grim-locations.json','/resource:outputs/v3/town-regions.json,town-regions.json','/resource:outputs/v3/updates-public-key.xml,updates-public-key.xml')
1..18 | ForEach-Object {
 $trialCode='D{0:D2}' -f $_
 $trialFile='outputs/Grim-Haritalari/'+$trialCode+'-Solo-Trial/'+$trialCode+'-Solo-Trial.json'
 if(!(Test-Path -LiteralPath $trialFile)){throw ('Missing captured Trial map: '+$trialCode)}
 $buildArgs+='/resource:'+$trialFile+',GrimFloor.'+$trialCode+'.SoloTrial.json'
}
Get-ChildItem -LiteralPath work/grim-embedded -Filter '*.jpg' | ForEach-Object {$buildArgs+='/resource:'+$_.FullName+',Grim.'+$_.Name}
$buildArgs+=@('/resource:outputs/v3/road-mesh.bin.gz,road-mesh.bin.gz','/resource:outputs/v3/transport-network.json,transport-network.json','/resource:outputs/v3/return-scrolls.json,return-scrolls.json')
$buildArgs+=@('/resource:outputs/NewEra-Kullanim-Kilavuzu.pdf,manual.pdf','/resource:outputs/Nanzhang/Nanzhang-NPC-Haritasi.jpg,nanzhang-map.jpg','/resource:outputs/Nanzhang/Nanzhang-Harita-Verisi.json,nanzhang-data.json')
$buildArgs+=@('outputs\v3\MemoryCoreV3.cs','outputs\v3\RulesV2.cs','outputs\v3\AutomationV3.cs','outputs\v3\V3Ui.cs','outputs\v3\V3App.cs','outputs\v3\DungeonRadar.cs','outputs\v3\GrimRooms.cs','outputs\v3\FloorNavigation.cs','outputs\v3\ObstacleNavigation.cs','outputs\v3\RunSettings.cs','outputs\v3\Hotkeys.cs','outputs\v3\AutoUpdate.cs','outputs\v3\AssemblyInfo.cs','outputs\v3\DualAccount.cs','outputs\v3\ModernUi.cs','outputs\v3\Expedition.cs','outputs\v3\CompletionNotice.cs','outputs\v3\MountTravel.cs','outputs\v3\LogView.cs','outputs\v3\RoadMesh.cs','outputs\v3\PhBotRoutes.cs')
$buildArgs+='work\V3Checks.cs'
$buildArgs+='work\NavigationChecks.cs'
$buildArgs+='work\GrimSurveyChecks.cs'
& $compiler @buildArgs
if($LASTEXITCODE -ne 0){throw 'V3 build failed'}
Write-Output 'V3 build OK'



