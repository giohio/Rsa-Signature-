# RSA Digital Signature Application

Ứng dụng ký số RSA với giao diện web và API backend.

## Cấu trúc dự án

- `Front_Rsa/Rsa_web`: Ứng dụng React frontend
- `RsaSignApi`: API backend .NET Core
- `Dockerfile`: File cấu hình Docker để đóng gói ứng dụng
- `docker-compose.yml`: File cấu hình Docker Compose để triển khai ứng dụng

## Yêu cầu hệ thống

- Docker và Docker Compose
- .NET SDK 8.0 (chỉ để phát triển)
- Node.js 20.x (chỉ để phát triển)

## Triển khai ứng dụng

### Sử dụng Docker

1. Clone repository:
   ```bash
   git clone <repository-url>
   cd Rsa_sign
   ```

2. Triển khai ứng dụng:

   **Trên Windows (PowerShell):**
   ```powershell
   .\run-docker.ps1
   ```

   **Trên Linux/macOS:**
   ```bash
   chmod +x run-docker.sh
   ./run-docker.sh
   ```

3. Truy cập ứng dụng tại: http://localhost:8080

### Triển khai thủ công

1. Build và khởi chạy các container:
   ```bash
   docker-compose build
   docker-compose up -d
   ```

2. Kiểm tra trạng thái:
   ```bash
   docker-compose ps
   ```

3. Dừng ứng dụng:
   ```bash
   docker-compose down
   ```

## Chạy ứng dụng không dùng Docker

Để chạy ứng dụng trực tiếp mà không dùng Docker:

1. **Trên Windows:**
   ```
   .\start-app.bat
   ```
   hoặc
   ```
   .\start-app.ps1
   ```

2. **Trên Linux/macOS:**
   ```
   chmod +x start-app.sh
   ./start-app.sh
   ```

## Cấu hình

Các biến môi trường có thể được cấu hình trong file `docker-compose.yml`:

- `ASPNETCORE_ENVIRONMENT`: Môi trường chạy ứng dụng (Development, Production)
- `ConnectionStrings__MongoDB`: Chuỗi kết nối MongoDB
- `MongoDB__DatabaseName`: Tên database MongoDB

## Phát triển

### Frontend

```bash
cd Front_Rsa/Rsa_web
npm install
npm run dev
```

### Backend

```bash
cd RsaSignApi
dotnet run
```

## Bảo mật

- Ứng dụng sử dụng mã hóa RSA để ký và xác thực chữ ký số
- Khóa riêng tư được lưu trữ an toàn trong cơ sở dữ liệu MongoDB
- Ứng dụng chạy trong Docker container với user không đặc quyền

## Giám sát

- Healthcheck được cấu hình cho cả API và MongoDB
- Truy cập API healthcheck tại: http://localhost:8080/api/health 