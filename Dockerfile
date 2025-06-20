# Stage 1: Build frontend
FROM node:20.19.2-slim AS frontend
RUN apt-get update && apt-get upgrade -y && rm -rf /var/lib/apt/lists/*
WORKDIR /app/frontend
COPY Front_Rsa/Rsa_web/package.json ./
RUN npm install && npm install @rollup/rollup-linux-x64-gnu lightningcss-linux-x64-gnu --no-save
COPY Front_Rsa/Rsa_web .
RUN npm run build:no-typecheck

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
# Copy only project file and restore
COPY RsaSignApi/RsaSignApi/*.csproj ./RsaSignApi/
RUN dotnet restore RsaSignApi/RsaSignApi.csproj
# Copy backend source files
COPY RsaSignApi/RsaSignApi/ ./RsaSignApi/
WORKDIR /src/RsaSignApi
# Publish to /app/publish with assembly info disabled
RUN dotnet publish RsaSignApi.csproj -c Release -o /app/publish /p:GenerateAssemblyInfo=false /p:GenerateTargetFrameworkAttribute=false

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Install LibreOffice with minimal dependencies for PDF conversion
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      libreoffice \
      libreoffice-core \
      libreoffice-writer \
      default-jre-headless \
      curl \
      ca-certificates \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# Create a directory for temporary files with proper permissions
RUN mkdir -p /tmp/libreoffice-conversion && chmod 777 /tmp/libreoffice-conversion

WORKDIR /app
# Copy published backend to root
COPY --from=backend /app/publish ./
# Copy frontend build output into wwwroot
COPY --from=frontend /app/frontend/dist ./wwwroot

# Thiết lập biến môi trường
ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
ENV TZ=Asia/Ho_Chi_Minh
ENV HOME=/tmp
ENV TMPDIR=/tmp/libreoffice-conversion

# Verify LibreOffice installation
RUN soffice --version

# Thiết lập healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost/api/health || exit 1

EXPOSE 80

# Thiết lập entry point
ENTRYPOINT ["dotnet", "RsaSignApi.dll"]