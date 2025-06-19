# Hiển thị banner
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  RSA Sign Application - Docker Deployment" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# Kiểm tra Docker và Docker Compose
Write-Host "Checking Docker..." -ForegroundColor Yellow
try {
    docker --version | Out-Null
    Write-Host "Docker is installed." -ForegroundColor Green
}
catch {
    Write-Host "Docker not found. Please install Docker first." -ForegroundColor Red
    exit 1
}

Write-Host ""

# Dừng và xóa các container cũ nếu có
Write-Host "Stopping any existing containers..." -ForegroundColor Yellow
docker-compose down

# Build và khởi động các container
Write-Host "Building and starting containers..." -ForegroundColor Yellow
docker-compose build
docker-compose up -d

# Kiểm tra trạng thái
Write-Host "Checking container status..." -ForegroundColor Yellow
Start-Sleep -Seconds 5
docker-compose ps

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Green
Write-Host "  Deployment completed!" -ForegroundColor Green
Write-Host "  Access the application at: http://localhost:8080" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green 