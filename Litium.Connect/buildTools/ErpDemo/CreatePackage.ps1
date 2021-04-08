function removeCredentials([string]$config){
	$xml = [xml](Get-Content $config)
	
	$credentials = @{
		MS_WebHookReceiverSecret_Litium  = ''
		WebHookSelfRegistrationCallbackHost = ''
		WebHookSelfRegistrationHost = ''
		WebHookSelfRegistrationClientId = ''
		WebHookSelfRegistrationClientSecret = ''
	}
	
	foreach($key in $credentials.Keys)
	{
		if(($addKey = $xml.SelectSingleNode("//appSettings/add[@key = '$key']")))
		{
			$addKey.SetAttribute('value',$credentials[$key])
		}
	}

    $xml.Save($config) 
}

$basePath = $PSScriptRoot
$deployPath = (join-path $basePath "Deploy")

Set-Location (join-path $basePath ../..)

Remove-Item $deployPath  -Force -Recurse -ErrorAction SilentlyContinue | Out-Null
New-Item $deployPath  -ItemType directory -ErrorAction SilentlyContinue | Out-Null

Copy-Item "samples\Litium.SampleApps.ErpDemo" $deployPath  -recurse -Force

Get-ChildItem $deployPath -Recurse | foreach {$_.Attributes = 'Normal'}
Remove-Item "$deployPath\Litium.SampleApps.ErpDemo\.vs" -Force -Recurse -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$deployPath\Litium.SampleApps.ErpDemo\packages" -Force -Recurse -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$deployPath\Litium.SampleApps.ErpDemo\src\WebHookEvents" -Force -Recurse -ErrorAction SilentlyContinue | Out-Null
Remove-Item "$deployPath\Litium.SampleApps.ErpDemo\src\*.log" -Force -Recurse -ErrorAction SilentlyContinue | Out-Null
removeCredentials("$deployPath\Litium.SampleApps.ErpDemo\src\Litium.SampleApps.ErpDemo\Web.config")

Copy-Item "tests\Postman Request Sample" "$deployPath\tests\Postman Request Sample" -recurse -Force

Get-ChildItem $deployPath -Recurse -Filter "*.csproj" | % {
    Remove-Item (join-path $_.Directory.FullName "bin") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (join-path $_.Directory.FullName "obj") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (join-path $_.Directory.FullName "*.vspscc") -Recurse -Force -ErrorAction SilentlyContinue
}

$nowStr = ((get-date).ToLocalTime()).ToString("yyyyMMddHHmmss")
Compress-Archive -Path "$deployPath\*" -DestinationPath "$deployPath\ErpDemo_$nowStr.zip"

Set-Location $basePath