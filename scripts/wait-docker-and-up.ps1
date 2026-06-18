Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe" -ErrorAction SilentlyContinue
$ready = $false
for ($i = 0; $i -lt 72; $i++) {
    Start-Sleep -Seconds 5
    docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        $ready = $true
        break
    }
}
if ($ready) {
    Set-Location $PSScriptRoot
    docker compose up -d 2>&1 | Out-File -FilePath docker-up2.log -Encoding utf8
    "DOCKER_READY" | Out-File -FilePath docker-up2.log -Append -Encoding utf8
} else {
    "DOCKER_NOT_READY" | Out-File -FilePath docker-up2.log -Encoding utf8
}
