# 一键构建：在 __terraria_mod_dev 目录下运行 build.ps1（自动处理 .NET 8 SDK 路径）
powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\..\build.ps1" -Project "$PSScriptRoot\RelicAnyMode.csproj"
