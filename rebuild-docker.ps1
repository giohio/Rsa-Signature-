# PowerShell script to rebuild and restart Docker containers

# Stop running containers
Write-Host "Stopping running containers..." -ForegroundColor Cyan
docker-compose down

# Build new images
Write-Host "Building new images..." -ForegroundColor Cyan
docker-compose build --no-cache

# Start containers
Write-Host "Starting containers..." -ForegroundColor Cyan
docker-compose up -d

# Show running containers
Write-Host "Running containers:" -ForegroundColor Cyan
docker-compose ps

# Follow logs
Write-Host "Following logs (press Ctrl+C to exit)..." -ForegroundColor Cyan
docker-compose logs -f 