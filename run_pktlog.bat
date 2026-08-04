@echo off
REM Lanza el GameServer con el log de TODOS los paquetes salientes/entrantes ([PKT-OUT]/[PKT-IN]).
REM Comparar: (A) crear arena directo vs (B) crear otro modo y cambiar a arena.
set SANTANA_PACKETLOG=1
cd /d "C:\emulador_cs\emuladorAnticheat\src\GameServer\bin\LatestOld_Release\net10.0"
GameServer.exe
