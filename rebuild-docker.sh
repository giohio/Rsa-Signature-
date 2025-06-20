#!/bin/bash

# Stop running containers
echo "Stopping running containers..."
docker-compose down

# Build new images
echo "Building new images..."
docker-compose build --no-cache

# Start containers
echo "Starting containers..."
docker-compose up -d

# Show running containers
echo "Running containers:"
docker-compose ps

# Follow logs
echo "Following logs (press Ctrl+C to exit)..."
docker-compose logs -f 