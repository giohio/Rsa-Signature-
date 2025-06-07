# Multi-stage Dockerfile for .NET backend and Vite/React frontend

# Stage 1: Build frontend
FROM node:20.19.2-alpine3.18 AS frontend
RUN apk update && apk upgrade --available --no-cache
WORKDIR /app/frontend
COPY Front_Rsa/Rsa_web/package*.json ./
RUN npm ci
COPY Front_Rsa/Rsa_web .
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS backend
WORKDIR /app/backend
# Copy csproj and restore dependencies
COPY RsaSignApi/RsaSignApi/*.csproj ./RsaSignApi/
RUN dotnet restore RsaSignApi/RsaSignApi.csproj
# Copy remaining source and publish
COPY . .
RUN dotnet publish RsaSignApi/RsaSignApi.csproj -c Release -o /app/publish

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app
# Copy published backend
COPY --from=backend /app/publish ./backend
# Copy frontend build output into backend/wwwroot
RUN mkdir -p backend/wwwroot
COPY --from=frontend /app/frontend/dist ./backend/wwwroot

EXPOSE 80
ENTRYPOINT ["dotnet", "backend/RsaSignApi.dll"]
