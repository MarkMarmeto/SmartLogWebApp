#!/bin/bash

echo "🚀 SmartLog Migration & Deployment Script"
echo "=========================================="
echo ""

# Stop and remove existing containers
echo "📦 Stopping existing containers..."
docker-compose down

# Rebuild and start containers
echo "🔨 Building and starting containers..."
docker-compose up --build -d

# Wait for database to be ready
echo "⏳ Waiting for database to be ready..."
sleep 10

# Show logs
echo ""
echo "📋 Application logs:"
echo "==================="
docker-compose logs -f smartlog-web
