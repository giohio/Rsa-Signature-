@echo off
echo Starting RSA Sign Application...
echo.

echo Starting Backend API...
start cmd /k "cd RsaSignApi\RsaSignApi && dotnet run"

timeout /t 5

echo Starting Frontend...
start cmd /k "cd Front_Rsa\Rsa_web && npm run dev"

echo.
echo Application started! Check the opened command windows for details.
echo. 