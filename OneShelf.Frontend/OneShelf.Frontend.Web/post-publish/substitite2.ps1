function Naive-Convert-HashToByteArray {
    [CmdletBinding()]
    Param ( [Parameter(Mandatory = $True, ValueFromPipeline = $True)] [String] $String )
    $String -split '([A-F0-9]{2})' | foreach-object { if ($_) {[System.Convert]::ToByte($_,16)}}
}

$filehash_as_bytes1 = Naive-Convert-HashToByteArray((Get-FileHash .\appsettings.json.bak).Hash)
$filehash_as_bytes2 = Naive-Convert-HashToByteArray((Get-FileHash .\appsettings.json).Hash)
$base641 = [System.Convert]::ToBase64String($filehash_as_bytes1).Replace("/", "\/")
$base642 = [System.Convert]::ToBase64String($filehash_as_bytes2).Replace("/", "\/")

$content = (Get-Content .\service-worker-assets.js).Replace($base641, $base642)
Set-Content .\service-worker-assets.js -Value $content

$content = (Get-Content .\service-worker-assets-compat.js).Replace($base641, $base642)
Set-Content .\service-worker-assets-compat.js -Value $content