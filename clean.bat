@echo off

for /d %%d in ("*") do rmdir /s /q "%%d\bin"
for /d %%d in ("*") do rmdir /s /q "%%d\obj"

rmdir /s /q "packages"
rmdir /s /q "TestResults"
