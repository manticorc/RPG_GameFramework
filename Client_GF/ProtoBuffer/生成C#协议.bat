@echo off
set des=./to

if exist "%des%" rd /s /q "%des%"
md "%des%"

for %%i in (./*.proto) do (
    protoc  --csharp_out="%des%" %%i
    rem ���������¶���ע�ͣ��ɺ���
    echo From %%i To %%~ni.cs Successfully!  
)
pause