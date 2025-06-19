#!/bin/bash

# Hiển thị banner
echo "====================================================="
echo "  RSA Sign Application - Docker Deployment"
echo "====================================================="

# Kiểm tra Docker
echo "Checking Docker..."
if ! command -v docker &> /dev/null; then
    echo "Docker not found. Please install Docker first."
    exit 1
fi

echo "Docker is installed."
echo

# Dừng và xóa các container cũ nếu có
echo "Stopping any existing containers..."
docker-compose down

# Build và khởi động các container
echo "Building and starting containers..."
docker-compose build
docker-compose up -d

# Kiểm tra trạng thái
echo "Checking container status..."
sleep 5
docker-compose ps

echo
echo "====================================================="
echo "  Deployment completed!"
echo "  Access the application at: http://localhost:8080"
echo "=====================================================" 